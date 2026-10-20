using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using CCB.API.Data;
using CCB.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly CCBDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ProfilesController(
        CCBDbContext context,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _context = context;
        _configuration = configuration;
        _environment = environment;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var tokenType =
            User.FindFirstValue("tokenType");

        if (tokenType != "access")
        {
            return Unauthorized(new
            {
                message =
                    "A valid access token is required."
            });
        }

        var profileIdValue =
            User.FindFirstValue("profileId");

        if (!int.TryParse(
                profileIdValue,
                out var profileId))
        {
            return Unauthorized(new
            {
                message =
                    "The access token does not contain a valid profile."
            });
        }

        var profile =
            await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.ProfileId == profileId
                );

        if (profile == null)
        {
            return NotFound(new
            {
                message =
                    "The profile could not be found."
            });
        }

        return Ok(new
        {
            user = new
            {
                profileId =
                    profile.ProfileId,

                name =
                    profile.Name,

                username =
                    profile.Username,

                phone =
                    profile.Phone,

                language =
                    profile.Language,

                avatarColor =
                    profile.AvatarColor,

                profilePhotoUrl =
                    profile.ProfilePhotoUrl,

                balance =
                    profile.Balance,

                darkMode =
                    profile.DarkMode,

                createdAt =
                    profile.CreatedAt,

                updatedAt =
                    profile.UpdatedAt
            }
        });
    }

    [Authorize]
    [HttpPut("me")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateMyProfile(
        [FromForm] UpdateProfileRequest request)
    {
        var tokenType =
            User.FindFirstValue("tokenType");

        if (tokenType != "access")
        {
            return Unauthorized(new
            {
                message =
                    "A valid access token is required."
            });
        }

        var profileIdValue =
            User.FindFirstValue("profileId");

        if (!int.TryParse(
                profileIdValue,
                out var profileId))
        {
            return Unauthorized(new
            {
                message =
                    "The access token does not contain a valid profile."
            });
        }

        var profile =
            await _context.Profiles
                .FirstOrDefaultAsync(
                    p => p.ProfileId == profileId
                );

        if (profile == null)
        {
            return NotFound(new
            {
                message =
                    "The profile could not be found."
            });
        }

        var name =
            request.Name?.Trim() ??
            string.Empty;

        var username =
            request.Username?
                .Trim()
                .ToLowerInvariant()
            ?? string.Empty;

        if (name.Length < 2 ||
            name.Length > 100)
        {
            return BadRequest(new
            {
                message =
                    "Full name must contain between 2 and 100 characters."
            });
        }

        if (!Regex.IsMatch(
                username,
                @"^[a-z0-9_]{3,30}$"))
        {
            return BadRequest(new
            {
                message =
                    "Username must contain 3 to 30 lowercase letters, numbers, or underscores."
            });
        }

        var usernameTaken =
            await _context.Profiles
                .AnyAsync(other =>
                    other.ProfileId != profileId &&
                    other.Username == username
                );

        if (usernameTaken)
        {
            return Conflict(new
            {
                message =
                    "This username is already taken."
            });
        }

        if (request.Photo != null)
        {
            var validationError =
                ValidatePhoto(request.Photo);

            if (validationError != null)
            {
                return BadRequest(new
                {
                    message = validationError
                });
            }
        }

        var avatarColor =
            IsValidHexColor(
                request.AvatarColor
            )
                ? request.AvatarColor!
                    .ToUpperInvariant()
                : profile.AvatarColor ??
                  "#0B654B";

        var previousPhotoUrl =
            profile.ProfilePhotoUrl;

        string? newlySavedPhotoPath = null;

        if (request.Photo != null)
        {
            var savedPhoto =
                await SaveProfilePhoto(
                    request.Photo
                );

            profile.ProfilePhotoUrl =
                savedPhoto.Url;

            newlySavedPhotoPath =
                savedPhoto.FilePath;
        }
        else if (request.RemovePhoto)
        {
            profile.ProfilePhotoUrl = null;
        }

        profile.Name = name;
        profile.Username = username;
        profile.AvatarColor = avatarColor;
        profile.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context
                .SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            DeleteFileIfItExists(
                newlySavedPhotoPath
            );

            return Conflict(new
            {
                message =
                    "The profile could not be updated. The username may already be registered."
            });
        }

        if (
            previousPhotoUrl != null &&
            previousPhotoUrl !=
                profile.ProfilePhotoUrl
        )
        {
            DeleteStoredProfilePhoto(
                previousPhotoUrl
            );
        }

        return Ok(new
        {
            message =
                "Profile updated successfully.",

            user = new
            {
                profileId =
                    profile.ProfileId,

                name =
                    profile.Name,

                username =
                    profile.Username,

                phone =
                    profile.Phone,

                language =
                    profile.Language,

                avatarColor =
                    profile.AvatarColor,

                profilePhotoUrl =
                    profile.ProfilePhotoUrl,

                balance =
                    profile.Balance,

                darkMode =
                    profile.DarkMode,

                createdAt =
                    profile.CreatedAt,

                updatedAt =
                    profile.UpdatedAt
            }
        });
    }

    [Authorize]
    [HttpPost("complete")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CompleteProfile(
        [FromForm] CompleteProfileRequest request)
    {
        var tokenType =
            User.FindFirstValue("tokenType");

        if (tokenType != "profile_setup")
        {
            return Unauthorized(new
            {
                message =
                    "A valid profile setup token is required."
            });
        }

        var phone =
            User.FindFirstValue("phone");

        if (string.IsNullOrWhiteSpace(phone))
        {
            return Unauthorized(new
            {
                message =
                    "The verified phone number is missing."
            });
        }

        var name =
            request.Name?.Trim() ??
            string.Empty;

        var username =
            request.Username?
                .Trim()
                .ToLowerInvariant()
            ?? string.Empty;

        if (name.Length < 2 ||
            name.Length > 100)
        {
            return BadRequest(new
            {
                message =
                    "Full name must contain between 2 and 100 characters."
            });
        }

        if (!Regex.IsMatch(
                username,
                @"^[a-z0-9_]{3,30}$"))
        {
            return BadRequest(new
            {
                message =
                    "Username must contain 3 to 30 lowercase letters, numbers, or underscores."
            });
        }

        var profileAlreadyExists =
            await _context.Profiles
                .AnyAsync(profile =>
                    profile.Phone == phone);

        if (profileAlreadyExists)
        {
            return Conflict(new
            {
                message =
                    "A profile already exists for this phone number."
            });
        }

        var usernameTaken =
            await _context.Profiles
                .AnyAsync(profile =>
                    profile.Username == username);

        if (usernameTaken)
        {
            return Conflict(new
            {
                message =
                    "This username is already taken."
            });
        }

        var avatarColor =
            IsValidHexColor(
                request.AvatarColor
            )
                ? request.AvatarColor!
                    .ToUpperInvariant()
                : "#0B654B";

        string? profilePhotoUrl = null;
        string? savedPhotoPath = null;

        if (request.Photo != null)
        {
            var validationError =
                ValidatePhoto(
                    request.Photo
                );

            if (validationError != null)
            {
                return BadRequest(new
                {
                    message =
                        validationError
                });
            }

            var savedPhoto =
                await SaveProfilePhoto(
                    request.Photo
                );

            profilePhotoUrl =
                savedPhoto.Url;

            savedPhotoPath =
                savedPhoto.FilePath;
        }

        var profile = new Profile
        {
            Name = name,
            Username = username,
            Phone = phone,
            AvatarColor = avatarColor,
            ProfilePhotoUrl =
                profilePhotoUrl,
            Language = "English",
            Balance = 0,
            DarkMode = false,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _context.Profiles.Add(profile);

            await _context
                .SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (
                savedPhotoPath != null &&
                System.IO.File.Exists(
                    savedPhotoPath
                )
            )
            {
                System.IO.File.Delete(
                    savedPhotoPath
                );
            }

            return Conflict(new
            {
                message =
                    "The profile could not be created. The phone number or username may already be registered."
            });
        }

        var accessToken =
            CreateAccessToken(profile);

        return Ok(new
        {
            message =
                "CCB profile created successfully.",

            nextScreen = "Home",

            token = accessToken,

            user = new
            {
                profileId =
                    profile.ProfileId,

                name =
                    profile.Name,

                username =
                    profile.Username,

                phone =
                    profile.Phone,

                language =
                    profile.Language,

                avatarColor =
                    profile.AvatarColor,

                profilePhotoUrl =
                    profile.ProfilePhotoUrl,

                balance =
                    profile.Balance,

                darkMode =
                    profile.DarkMode
            }
        });
    }

    private string? ValidatePhoto(
        IFormFile photo)
    {
        if (photo.Length == 0)
        {
            return
                "The selected profile photo is empty.";
        }

        const long maximumPhotoSize =
            5 * 1024 * 1024;

        if (photo.Length > maximumPhotoSize)
        {
            return
                "The profile photo cannot exceed 5 MB.";
        }

        var allowedContentTypes =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase)
            {
                "image/jpeg",
                "image/png",
                "image/webp"
            };

        if (!allowedContentTypes.Contains(
                photo.ContentType))
        {
            return
                "Only JPG, PNG, and WebP photos are allowed.";
        }

        return null;
    }

    private async Task<SavedPhoto>
        SaveProfilePhoto(
            IFormFile photo)
    {
        var extension =
            photo.ContentType
                .ToLowerInvariant()
            switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",

                _ => throw new
                    InvalidOperationException(
                        "Unsupported photo type."
                    )
            };

        var fileName =
            $"{Guid.NewGuid():N}{extension}";

        var webRoot =
            _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(
                webRoot))
        {
            webRoot = Path.Combine(
                _environment
                    .ContentRootPath,
                "wwwroot"
            );
        }

        var uploadDirectory =
            Path.Combine(
                webRoot,
                "uploads",
                "profiles"
            );

        Directory.CreateDirectory(
            uploadDirectory
        );

        var filePath =
            Path.Combine(
                uploadDirectory,
                fileName
            );

        await using var stream =
            new FileStream(
                filePath,
                FileMode.CreateNew
            );

        await photo.CopyToAsync(stream);

        var relativeUrl =
            $"/uploads/profiles/{fileName}";

        var absoluteUrl =
            $"{Request.Scheme}://" +
            $"{Request.Host}" +
            relativeUrl;

        return new SavedPhoto(
            absoluteUrl,
            filePath
        );
    }

    private void DeleteStoredProfilePhoto(
        string photoUrl)
    {
        var urlPath = photoUrl;

        if (Uri.TryCreate(
                photoUrl,
                UriKind.Absolute,
                out var absoluteUri))
        {
            urlPath = absoluteUri.AbsolutePath;
        }

        var fileName =
            Path.GetFileName(urlPath);

        if (string.IsNullOrWhiteSpace(
                fileName))
        {
            return;
        }

        var webRoot =
            _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(
                webRoot))
        {
            webRoot = Path.Combine(
                _environment.ContentRootPath,
                "wwwroot"
            );
        }

        var filePath = Path.Combine(
            webRoot,
            "uploads",
            "profiles",
            fileName
        );

        DeleteFileIfItExists(filePath);
    }

    private static void DeleteFileIfItExists(
        string? filePath)
    {
        if (
            !string.IsNullOrWhiteSpace(
                filePath
            ) &&
            System.IO.File.Exists(
                filePath
            )
        )
        {
            try
            {
                System.IO.File.Delete(
                    filePath
                );
            }
            catch (IOException)
            {
                // File cleanup must not undo a
                // successful profile update.
            }
            catch (UnauthorizedAccessException)
            {
                // File cleanup must not undo a
                // successful profile update.
            }
        }
    }

    private string CreateAccessToken(
        Profile profile)
    {
        var jwtKey =
            _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(
                jwtKey))
        {
            throw new
                InvalidOperationException(
                    "Jwt:Key is missing."
                );
        }

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                profile.ProfileId.ToString()
            ),

            new Claim(
                "profileId",
                profile.ProfileId.ToString()
            ),

            new Claim(
                "phone",
                profile.Phone ??
                string.Empty
            ),

            new Claim(
                "username",
                profile.Username ??
                string.Empty
            ),

            new Claim(
                "tokenType",
                "access"
            )
        };

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    jwtKey
                )
            );

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

        var expirationMinutes =
            int.TryParse(
                _configuration[
                    "Jwt:ExpirationMinutes"
                ],
                out var minutes
            )
                ? minutes
                : 1440;

        var token =
            new JwtSecurityToken(
                issuer:
                    _configuration[
                        "Jwt:Issuer"
                    ],

                audience:
                    _configuration[
                        "Jwt:Audience"
                    ],

                claims: claims,

                expires:
                    DateTime.UtcNow
                        .AddMinutes(
                            expirationMinutes
                        ),

                signingCredentials:
                    credentials
            );

        return new
            JwtSecurityTokenHandler()
                .WriteToken(token);
    }

    private static bool IsValidHexColor(
        string? color)
    {
        return
            !string.IsNullOrWhiteSpace(
                color
            ) &&
            Regex.IsMatch(
                color,
                @"^#[0-9A-Fa-f]{6}$"
            );
    }

    private sealed record SavedPhoto(
        string Url,
        string FilePath
    );
}

public class CompleteProfileRequest
{
    public string Name { get; set; } =
        string.Empty;

    public string Username { get; set; } =
        string.Empty;

    public string? AvatarColor {
        get;
        set;
    }

    public IFormFile? Photo {
        get;
        set;
    }
}

public class UpdateProfileRequest
{
    public string Name { get; set; } =
        string.Empty;

    public string Username { get; set; } =
        string.Empty;

    public string? AvatarColor {
        get;
        set;
    }

    public IFormFile? Photo {
        get;
        set;
    }

    public bool RemovePhoto {
        get;
        set;
    }
}
