using System.Security.Claims;
using CCB.API.Data;
using CCB.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BusinessesController : ControllerBase
{
    private readonly CCBDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public BusinessesController(
        CCBDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: api/Businesses/mine
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyBusiness()
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var business = await _context.BusinessOwners
            .Where(owner => owner.ProfileId == profileId)
            .Select(owner => new
            {
                owner.Business.BusinessId,
                owner.Business.Name,
                owner.Business.Category,
                owner.Business.Rating,
                owner.Business.BannerColor,
                owner.Business.BannerImageUrl,
                owner.Business.Description,
                owner.Business.Address,
                owner.Business.Phone,
                owner.Business.Featured,
                owner.Business.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (business is null)
        {
            return NotFound(new { message = "You have not created a store yet." });
        }

        return Ok(new { business });
    }

    // POST: api/Businesses
    [HttpPost]
    public async Task<IActionResult> CreateBusiness(CreateBusinessRequest request)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new
            {
                message = "Business name and category are required."
            });
        }

        var alreadyOwnsBusiness = await _context.BusinessOwners
            .AnyAsync(owner => owner.ProfileId == profileId);

        if (alreadyOwnsBusiness)
        {
            return Conflict(new
            {
                message = "You already have a store."
            });
        }

        var profileExists = await _context.Profiles
            .AnyAsync(profile => profile.ProfileId == profileId);

        if (!profileExists)
        {
            return Unauthorized(new { message = "Your profile could not be found." });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var business = new Business
        {
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Rating = 0m,
            BannerColor = string.IsNullOrWhiteSpace(request.BannerColor)
                ? "#1A6B4A"
                : request.BannerColor.Trim(),
            Description = CleanOptional(request.Description),
            Address = CleanOptional(request.Address),
            Phone = CleanOptional(request.Phone),
            Featured = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Businesses.Add(business);
        await _context.SaveChangesAsync();

        _context.BusinessOwners.Add(new BusinessOwner
        {
            BusinessId = business.BusinessId,
            ProfileId = profileId,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return CreatedAtAction(
            nameof(GetBusinessById),
            new { businessId = business.BusinessId },
            new
            {
                message = "Store created successfully.",
                business = new
                {
                    business.BusinessId,
                    business.Name,
                    business.Category,
                    business.Rating,
                    business.BannerColor,
                    business.BannerImageUrl,
                    business.Description,
                    business.Address,
                    business.Phone,
                    business.Featured,
                    business.CreatedAt
                }
            });
    }

    // GET: api/Businesses/5
    [AllowAnonymous]
    [HttpGet("{businessId:int}")]
    public async Task<IActionResult> GetBusinessById(int businessId)
    {
        var business = await _context.Businesses
            .Where(item => item.BusinessId == businessId)
            .Select(item => new
            {
                item.BusinessId,
                item.Name,
                item.Category,
                item.Rating,
                item.BannerColor,
                item.BannerImageUrl,
                item.Description,
                item.Address,
                item.Phone,
                item.Featured,
                item.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        return Ok(new { business });
    }

    // PUT: api/Businesses/5
    [HttpPut("{businessId:int}")]
    public async Task<IActionResult> UpdateBusiness(
        int businessId,
        UpdateBusinessRequest request)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var ownsBusiness = await _context.BusinessOwners.AnyAsync(owner =>
            owner.BusinessId == businessId && owner.ProfileId == profileId);

        if (!ownsBusiness)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new
            {
                message = "Business name and category are required."
            });
        }

        var business = await _context.Businesses.FindAsync(businessId);

        if (business is null)
        {
            return NotFound(new { message = "Business not found." });
        }

        business.Name = request.Name.Trim();
        business.Category = request.Category.Trim();
        business.BannerColor = string.IsNullOrWhiteSpace(request.BannerColor)
            ? "#1A6B4A"
            : request.BannerColor.Trim();
        business.Description = CleanOptional(request.Description);
        business.Address = CleanOptional(request.Address);
        business.Phone = CleanOptional(request.Phone);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Store updated successfully.",
            business = new
            {
                business.BusinessId,
                business.Name,
                business.Category,
                business.Rating,
                business.BannerColor,
                business.BannerImageUrl,
                business.Description,
                business.Address,
                business.Phone,
                business.Featured,
                business.CreatedAt
            }
        });
    }

    // POST: api/Businesses/mine/banner-image
    [HttpPost("mine/banner-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadBannerImage(IFormFile image)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var business = await _context.BusinessOwners
            .Where(owner => owner.ProfileId == profileId)
            .Select(owner => owner.Business)
            .FirstOrDefaultAsync();

        if (business is null)
        {
            return NotFound(new { message = "Create your store before uploading a banner." });
        }

        var validationError = ValidateImage(image);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var fileName = $"business-{business.BusinessId}-{Guid.NewGuid():N}{extension}";
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, "uploads", "businesses");
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);
        await using (var stream = System.IO.File.Create(filePath))
        {
            await image.CopyToAsync(stream);
        }

        business.BannerImageUrl = $"/uploads/businesses/{fileName}";
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Store banner uploaded successfully.",
            bannerImageUrl = business.BannerImageUrl
        });
    }

    // DELETE: api/Businesses/mine
    [HttpDelete("mine")]
    public async Task<IActionResult> DeleteMyBusiness()
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var ownership = await _context.BusinessOwners
            .FirstOrDefaultAsync(owner => owner.ProfileId == profileId);

        if (ownership is null)
        {
            return NotFound(new { message = "You do not have a store to delete." });
        }

        var business = await _context.Businesses
            .FirstOrDefaultAsync(item => item.BusinessId == ownership.BusinessId);

        if (business is null)
        {
            return NotFound(new { message = "Store not found." });
        }

        var productIds = await _context.Products
            .Where(product => product.BusinessId == business.BusinessId)
            .Select(product => product.ProductId)
            .ToListAsync();

        var hasOrderHistory = await _context.OrderItems
            .AnyAsync(item => productIds.Contains(item.ProductId));

        if (hasOrderHistory)
        {
            return Conflict(new
            {
                message = "This store has order history and cannot be permanently deleted. Hide its products instead."
            });
        }

        var products = await _context.Products
            .Where(product => product.BusinessId == business.BusinessId)
            .ToListAsync();

        var services = await _context.BusinessServices
            .Where(service => service.BusinessId == business.BusinessId)
            .ToListAsync();

        var owners = await _context.BusinessOwners
            .Where(owner => owner.BusinessId == business.BusinessId)
            .ToListAsync();

        var productImages = products
            .Select(product => product.ImageUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToList();

        var bannerImage = business.BannerImageUrl;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Products.RemoveRange(products);
        _context.BusinessServices.RemoveRange(services);
        _context.BusinessOwners.RemoveRange(owners);
        _context.Businesses.Remove(business);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        foreach (var imageUrl in productImages)
        {
            DeleteUploadedImage(imageUrl, "products");
        }

        DeleteUploadedImage(bannerImage, "businesses");

        return Ok(new
        {
            message = "Store deleted successfully.",
            businessId = business.BusinessId
        });
    }

    private bool TryGetProfileId(out int profileId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("profileId")
            ?? User.FindFirstValue("sub");

        return int.TryParse(value, out profileId);
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ValidateImage(IFormFile image)
    {
        if (image is null || image.Length == 0)
        {
            return "Choose an image to upload.";
        }

        if (image.Length > 5 * 1024 * 1024)
        {
            return "The image must be 5 MB or smaller.";
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

        return allowedExtensions.Contains(extension)
            ? null
            : "Only JPG, PNG and WEBP images are allowed.";
    }

    private void DeleteUploadedImage(string? imageUrl, string folder)
    {
        var expectedPrefix = $"/uploads/{folder}/";

        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !imageUrl.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(imageUrl);
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", folder, fileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
}

public class CreateBusinessRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? BannerColor { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
}

public class UpdateBusinessRequest : CreateBusinessRequest
{
}
