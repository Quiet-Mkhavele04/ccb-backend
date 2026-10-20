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
public class ProductsController : ControllerBase
{
    private readonly CCBDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(
        CCBDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: api/Products?search=bread&category=Food&businessId=1&featuredOnly=true
    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int? businessId,
        [FromQuery] bool featuredOnly = false)
    {
        var query = _context.Products
            .AsNoTracking()
            .Where(product => product.IsAvailable)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchText = search.Trim();
            query = query.Where(product =>
                product.Name.Contains(searchText) ||
                (product.Description != null && product.Description.Contains(searchText)) ||
                product.Business.Name.Contains(searchText));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryText = category.Trim();
            query = query.Where(product => product.Category == categoryText);
        }

        if (businessId.HasValue)
        {
            query = query.Where(product => product.BusinessId == businessId.Value);
        }

        if (featuredOnly)
        {
            query = query.Where(product => product.IsFeatured);
        }

        var products = await query
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.Name)
            .Select(product => new
            {
                product.ProductId,
                product.BusinessId,
                BusinessName = product.Business.Name,
                BusinessBannerColor = product.Business.BannerColor,
                BusinessBannerImageUrl = product.Business.BannerImageUrl,
                product.Name,
                product.Description,
                product.Category,
                product.Price,
                product.ImageUrl,
                product.StockQuantity,
                product.IsAvailable,
                product.IsFeatured
            })
            .ToListAsync();

        return Ok(new { products });
    }

    // GET: api/Products/mine (includes unavailable products)
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyProducts()
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var business = await _context.BusinessOwners
            .AsNoTracking()
            .Where(owner => owner.ProfileId == profileId)
            .Select(owner => new
            {
                owner.BusinessId,
                owner.Business.Name
            })
            .FirstOrDefaultAsync();

        if (business is null)
        {
            return NotFound(new { message = "Create your store before managing products." });
        }

        var products = await _context.Products
            .AsNoTracking()
            .Where(product => product.BusinessId == business.BusinessId)
            .OrderByDescending(product => product.CreatedAt)
            .Select(product => new
            {
                product.ProductId,
                product.BusinessId,
                product.Name,
                product.Description,
                product.Category,
                product.Price,
                product.ImageUrl,
                product.StockQuantity,
                product.IsAvailable,
                product.IsFeatured,
                product.CreatedAt,
                product.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { business, products });
    }

    // GET: api/Products/5
    [HttpGet("{productId:int}")]
    public async Task<IActionResult> GetProduct(int productId)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Where(item => item.ProductId == productId && item.IsAvailable)
            .Select(item => new
            {
                item.ProductId,
                item.BusinessId,
                BusinessName = item.Business.Name,
                BusinessCategory = item.Business.Category,
                BusinessRating = item.Business.Rating,
                BusinessBannerColor = item.Business.BannerColor,
                BusinessBannerImageUrl = item.Business.BannerImageUrl,
                BusinessAddress = item.Business.Address,
                BusinessPhone = item.Business.Phone,
                item.Name,
                item.Description,
                item.Category,
                item.Price,
                item.ImageUrl,
                item.StockQuantity,
                item.IsAvailable,
                item.IsFeatured
            })
            .FirstOrDefaultAsync();

        return product is null
            ? NotFound(new { message = "Product not found." })
            : Ok(new { product });
    }

    // GET: api/Products/business/1
    [HttpGet("business/{businessId:int}")]
    public async Task<IActionResult> GetBusinessProducts(int businessId)
    {
        var businessExists = await _context.Businesses
            .AsNoTracking()
            .AnyAsync(business => business.BusinessId == businessId);

        if (!businessExists)
        {
            return NotFound(new { message = "Business not found." });
        }

        var products = await _context.Products
            .AsNoTracking()
            .Where(product => product.BusinessId == businessId && product.IsAvailable)
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.Name)
            .Select(product => new
            {
                product.ProductId,
                product.BusinessId,
                product.Name,
                product.Description,
                product.Category,
                product.Price,
                product.ImageUrl,
                product.StockQuantity,
                product.IsAvailable,
                product.IsFeatured
            })
            .ToListAsync();

        return Ok(new { products });
    }

    // POST: api/Products
    [HttpPost]
    public async Task<IActionResult> CreateProduct(CreateProductRequest request)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var businessId = await GetOwnedBusinessId(profileId);
        if (businessId is null)
        {
            return NotFound(new { message = "Create your store before adding products." });
        }

        var error = ValidateProduct(request.Name, request.Category, request.Price, request.StockQuantity);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var product = new Product
        {
            BusinessId = businessId.Value,
            Name = request.Name.Trim(),
            Description = CleanOptional(request.Description),
            Category = request.Category.Trim(),
            Price = request.Price,
            ImageUrl = null,
            StockQuantity = request.StockQuantity,
            IsAvailable = request.IsAvailable && request.StockQuantity > 0,
            IsFeatured = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetProduct),
            new { productId = product.ProductId },
            new
            {
                message = "Product created successfully.",
                product = ToProductResponse(product)
            });
    }

    // PUT: api/Products/5
    [HttpPut("{productId:int}")]
    public async Task<IActionResult> UpdateProduct(
        int productId,
        UpdateProductRequest request)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var product = await GetOwnedProduct(productId, profileId);
        if (product is null)
        {
            return NotFound(new { message = "Product not found in your store." });
        }

        var error = ValidateProduct(request.Name, request.Category, request.Price, request.StockQuantity);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        product.Name = request.Name.Trim();
        product.Description = CleanOptional(request.Description);
        product.Category = request.Category.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.IsAvailable = request.IsAvailable && request.StockQuantity > 0;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Product updated successfully.",
            product = ToProductResponse(product)
        });
    }

    // POST: api/Products/5/image
    [HttpPost("{productId:int}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProductImage(
        int productId,
        IFormFile image)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var product = await GetOwnedProduct(productId, profileId);
        if (product is null)
        {
            return NotFound(new { message = "Product not found in your store." });
        }

        var validationError = ValidateImage(image);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var fileName = $"product-{product.ProductId}-{Guid.NewGuid():N}{extension}";
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);
        await using (var stream = System.IO.File.Create(filePath))
        {
            await image.CopyToAsync(stream);
        }

        product.ImageUrl = $"/uploads/products/{fileName}";
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Product image uploaded successfully.",
            imageUrl = product.ImageUrl
        });
    }

    // PATCH: api/Products/5/availability
    [HttpPatch("{productId:int}/availability")]
    public async Task<IActionResult> SetAvailability(
        int productId,
        SetProductAvailabilityRequest request)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var product = await GetOwnedProduct(productId, profileId);
        if (product is null)
        {
            return NotFound(new { message = "Product not found in your store." });
        }

        if (request.IsAvailable && product.StockQuantity <= 0)
        {
            return BadRequest(new { message = "Add stock before making this product available." });
        }

        product.IsAvailable = request.IsAvailable;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = request.IsAvailable
                ? "Product is now available."
                : "Product is now hidden.",
            productId = product.ProductId,
            product.IsAvailable
        });
    }

    // DELETE: api/Products/5
    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> DeleteProduct(int productId)
    {
        if (!TryGetProfileId(out var profileId))
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var product = await GetOwnedProduct(productId, profileId);
        if (product is null)
        {
            return NotFound(new { message = "Product not found in your store." });
        }

        var hasOrderHistory = await _context.OrderItems
            .AnyAsync(item => item.ProductId == productId);

        if (hasOrderHistory)
        {
            return Conflict(new
            {
                message = "This product has order history and cannot be permanently deleted. Hide it from the marketplace instead."
            });
        }

        var imagePath = product.ImageUrl;
        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        DeleteUploadedImage(imagePath);

        return Ok(new
        {
            message = "Product deleted successfully.",
            productId
        });
    }

    private bool TryGetProfileId(out int profileId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("profileId")
            ?? User.FindFirstValue("sub");

        return int.TryParse(value, out profileId);
    }

    private async Task<int?> GetOwnedBusinessId(int profileId)
    {
        return await _context.BusinessOwners
            .Where(owner => owner.ProfileId == profileId)
            .Select(owner => (int?)owner.BusinessId)
            .FirstOrDefaultAsync();
    }

    private async Task<Product?> GetOwnedProduct(int productId, int profileId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(product =>
                product.ProductId == productId &&
                product.Business.BusinessOwners.Any(owner => owner.ProfileId == profileId));
    }

    private static string? ValidateProduct(
        string name,
        string category,
        decimal price,
        int stockQuantity)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
        {
            return "Product name and category are required.";
        }

        if (price < 0)
        {
            return "Price cannot be negative.";
        }

        return stockQuantity < 0 ? "Stock quantity cannot be negative." : null;
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

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static object ToProductResponse(Product product)
    {
        return new
        {
            product.ProductId,
            product.BusinessId,
            product.Name,
            product.Description,
            product.Category,
            product.Price,
            product.ImageUrl,
            product.StockQuantity,
            product.IsAvailable,
            product.IsFeatured,
            product.CreatedAt,
            product.UpdatedAt
        };
    }

    private void DeleteUploadedImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !imageUrl.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(imageUrl);
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", "products", fileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
}

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class UpdateProductRequest : CreateProductRequest
{
}

public class SetProductAvailabilityRequest
{
    public bool IsAvailable { get; set; }
}
