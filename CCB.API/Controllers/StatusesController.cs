using System.Security.Claims;
using CCB.API.Data;
using CCB.API.Hubs;
using CCB.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatusesController : ControllerBase
{
    private readonly CCBDbContext _context;
    private readonly IHubContext<ChatHub>
        _chatHub;
    private readonly IWebHostEnvironment
        _environment;

    public StatusesController(
        CCBDbContext context,
        IHubContext<ChatHub> chatHub,
        IWebHostEnvironment environment
    )
    {
        _context = context;
        _chatHub = chatHub;
        _environment = environment;
    }

    // GET: api/Statuses
    [HttpGet]
    public async Task<IActionResult> GetStatuses()
    {
        var profileId = GetCurrentProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message =
                    "A valid access token is required."
            });
        }

        var currentProfileId = profileId.Value;
        var now = DateTime.UtcNow;

        var contactIds =
            await _context.ProfileContacts
                .AsNoTracking()
                .Where(profileContact =>
                    profileContact.ProfileId ==
                        currentProfileId
                )
                .Select(profileContact =>
                    profileContact.ContactId
                )
                .ToListAsync();

        var ownContactId =
            await _context.Contacts
                .AsNoTracking()
                .Where(contact =>
                    contact.ProfileId ==
                        currentProfileId
                )
                .Select(contact =>
                    (int?)contact.ContactId
                )
                .FirstOrDefaultAsync();

        if (
            ownContactId.HasValue &&
            !contactIds.Contains(
                ownContactId.Value
            )
        )
        {
            contactIds.Add(ownContactId.Value);
        }

        var statuses =
            await _context.Statuses
                .AsNoTracking()
                .Where(status =>
                    contactIds.Contains(
                        status.ContactId
                    ) &&
                    (
                        status.ExpiresAt == null ||
                        status.ExpiresAt > now
                    )
                )
                .OrderByDescending(status =>
                    status.CreatedAt
                )
                .Select(status => new
                {
                    statusId =
                        status.StatusId,

                    contactId =
                        status.ContactId,

                    profileId =
                        status.Contact.ProfileId,

                    name =
                        status.Contact.Name,

                    avatarColor =
                        status.Contact.AvatarColor,

                    profilePhotoUrl =
                        _context.Profiles
                            .Where(profile =>
                                profile.ProfileId ==
                                status.Contact.ProfileId
                            )
                            .Select(profile =>
                                profile.ProfilePhotoUrl
                            )
                            .FirstOrDefault(),

                    mediaType =
                        status.MediaType,

                    mediaUrl =
                        status.MediaUrl,

                    caption =
                        status.Caption,

                    backgroundColor =
                        status.BackgroundColor,

                    createdAt =
                        status.CreatedAt,

                    expiresAt =
                        status.ExpiresAt,

                    viewed = status.Viewed,

                    isMine =
                        status.Contact.ProfileId ==
                        currentProfileId
                })
                .ToListAsync();

        return Ok(new
        {
            statuses
        });
    }

    // POST: api/Statuses
    [HttpPost]
    [RequestSizeLimit(104857600)]
    public async Task<IActionResult> CreateStatus(
        [FromForm] CreateStatusRequest request
    )
    {
        var profileId = GetCurrentProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message =
                    "A valid access token is required."
            });
        }

        var caption = request.Caption?.Trim();

        if (
            string.IsNullOrWhiteSpace(caption) &&
            request.Media == null
        )
        {
            return BadRequest(new
            {
                message =
                    "Enter text or choose a photo or video."
            });
        }

        if (caption?.Length > 500)
        {
            return BadRequest(new
            {
                message =
                    "A status cannot be longer than 500 characters."
            });
        }

        var backgroundColor =
            request.BackgroundColor?.Trim();

        if (
            string.IsNullOrWhiteSpace(
                backgroundColor
            )
        )
        {
            backgroundColor = "#0D5740";
        }

        if (!IsValidHexColor(backgroundColor))
        {
            return BadRequest(new
            {
                message =
                    "Enter a valid background colour such as #0D5740."
            });
        }

        var contact =
            await _context.Contacts
                .FirstOrDefaultAsync(contact =>
                    contact.ProfileId ==
                    profileId.Value
                );

        if (contact == null)
        {
            return BadRequest(new
            {
                message =
                    "Your CCB contact profile could not be found."
            });
        }

        var createdAt = DateTime.UtcNow;

        string? mediaType = null;
        string? mediaUrl = null;

        if (request.Media != null)
        {
            var contentType =
                request.Media.ContentType
                    .ToLowerInvariant();

            if (contentType.StartsWith("image/"))
            {
                mediaType = "image";
            }
            else if (contentType.StartsWith("video/"))
            {
                mediaType = "video";
            }
            else
            {
                return BadRequest(new
                {
                    message =
                        "Only image and video status files are supported."
                });
            }

            const long maximumImageBytes =
                15 * 1024 * 1024;

            const long maximumVideoBytes =
                100 * 1024 * 1024;

            var maximumBytes =
                mediaType == "video"
                    ? maximumVideoBytes
                    : maximumImageBytes;

            if (request.Media.Length > maximumBytes)
            {
                return BadRequest(new
                {
                    message = mediaType == "video"
                        ? "The video cannot be larger than 100 MB."
                        : "The image cannot be larger than 15 MB."
                });
            }

            var extension =
                Path.GetExtension(
                    request.Media.FileName
                ).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = mediaType == "video"
                    ? ".mp4"
                    : ".jpg";
            }

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var webRoot =
                _environment.WebRootPath ??
                Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot"
                );

            var uploadDirectory =
                Path.Combine(
                    webRoot,
                    "uploads",
                    "statuses"
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
                System.IO.File.Create(
                    filePath
                );

            await request.Media.CopyToAsync(
                stream
            );

            mediaUrl =
                $"/uploads/statuses/{fileName}";
        }

        var profilePhotoUrl =
            await _context.Profiles
                .AsNoTracking()
                .Where(item =>
                    item.ProfileId ==
                    profileId.Value
                )
                .Select(item =>
                    item.ProfilePhotoUrl
                )
                .FirstOrDefaultAsync();

        var status = new Status
        {
            ContactId = contact.ContactId,
            Caption = caption ?? string.Empty,
            BackgroundColor = backgroundColor,
            MediaType = mediaType,
            MediaUrl = mediaUrl,
            CreatedAt = createdAt,
            Viewed = false,
            ExpiresAt = createdAt.AddHours(24)
        };

        _context.Statuses.Add(status);

        await _context.SaveChangesAsync();

        await NotifyStatusAudienceAsync(
            contact.ContactId,
            profileId.Value,
            "created"
        );

        return Ok(new
        {
            message =
                "Status created successfully.",

            status = new
            {
                statusId = status.StatusId,
                contactId = status.ContactId,
                profileId = profileId.Value,
                name = contact.Name,
                avatarColor = contact.AvatarColor,
                profilePhotoUrl,
                mediaType = status.MediaType,
                mediaUrl = status.MediaUrl,
                caption = status.Caption,
                backgroundColor =
                    status.BackgroundColor,
                createdAt = status.CreatedAt,
                expiresAt = status.ExpiresAt,
                viewed = status.Viewed,
                isMine = true
            }
        });
    }

    // POST: api/Statuses/5/view
    [HttpPost("{statusId:long}/view")]
    public async Task<IActionResult> MarkViewed(
        long statusId
    )
    {
        var profileId = GetCurrentProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message =
                    "A valid access token is required."
            });
        }

        var status =
            await _context.Statuses
                .Include(item => item.Contact)
                .FirstOrDefaultAsync(item =>
                    item.StatusId == statusId
                );

        if (status == null)
        {
            return NotFound(new
            {
                message =
                    "The status was not found."
            });
        }

        var isMine =
            status.Contact.ProfileId ==
            profileId.Value;

        var isMyContact =
            await _context.ProfileContacts
                .AnyAsync(profileContact =>
                    profileContact.ProfileId ==
                        profileId.Value &&
                    profileContact.ContactId ==
                        status.ContactId
                );

        if (!isMine && !isMyContact)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You cannot view this status."
                }
            );
        }

        if (!status.Viewed)
        {
            status.Viewed = true;
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            message = "Status marked as viewed.",
            statusId,
            viewed = true
        });
    }

    // DELETE: api/Statuses/5
    [HttpDelete("{statusId:long}")]
    public async Task<IActionResult> DeleteStatus(
        long statusId
    )
    {
        var profileId = GetCurrentProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message =
                    "A valid access token is required."
            });
        }

        var status =
            await _context.Statuses
                .Include(item => item.Contact)
                .FirstOrDefaultAsync(item =>
                    item.StatusId == statusId
                );

        if (status == null)
        {
            return NotFound(new
            {
                message =
                    "The status was not found."
            });
        }

        if (
            status.Contact.ProfileId !=
            profileId.Value
        )
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You can only delete your own status."
                }
            );
        }

        _context.Statuses.Remove(status);

        DeleteStatusMediaFile(
            status.MediaUrl
        );

        await _context.SaveChangesAsync();

        await NotifyStatusAudienceAsync(
            status.ContactId,
            profileId.Value,
            "deleted"
        );

        return Ok(new
        {
            message =
                "Status deleted successfully.",

            statusId
        });
    }

    private int? GetCurrentProfileId()
    {
        var profileIdValue =
            User.FindFirstValue("profileId");

        return int.TryParse(
            profileIdValue,
            out var profileId
        )
            ? profileId
            : null;
    }

    private void DeleteStatusMediaFile(
        string? mediaUrl
    )
    {
        if (
            string.IsNullOrWhiteSpace(mediaUrl) ||
            !mediaUrl.StartsWith(
                "/uploads/statuses/",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        var webRoot =
            _environment.WebRootPath ??
            Path.Combine(
                _environment.ContentRootPath,
                "wwwroot"
            );

        var relativePath =
            mediaUrl.TrimStart('/')
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar
                );

        var filePath =
            Path.Combine(
                webRoot,
                relativePath
            );

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private async Task NotifyStatusAudienceAsync(
        int contactId,
        int ownerProfileId,
        string changeType
    )
    {
        var audienceProfileIds =
            await _context.ProfileContacts
                .AsNoTracking()
                .Where(profileContact =>
                    profileContact.ContactId ==
                    contactId
                )
                .Select(profileContact =>
                    profileContact.ProfileId
                )
                .Distinct()
                .ToListAsync();

        if (
            !audienceProfileIds.Contains(
                ownerProfileId
            )
        )
        {
            audienceProfileIds.Add(
                ownerProfileId
            );
        }

        var notification = new
        {
            ownerProfileId,
            changeType
        };

        var sendTasks =
            audienceProfileIds.Select(
                audienceProfileId =>
                    _chatHub.Clients
                        .Group(
                            $"profile-{audienceProfileId}"
                        )
                        .SendAsync(
                            "StatusesChanged",
                            notification
                        )
            );

        await Task.WhenAll(sendTasks);
    }

    private static bool IsValidHexColor(
        string color
    )
    {
        if (
            color.Length != 7 ||
            color[0] != '#'
        )
        {
            return false;
        }

        return color
            .Skip(1)
            .All(character =>
                Uri.IsHexDigit(character)
            );
    }
}

public class CreateStatusRequest
{
    public string? Caption { get; set; }

    public string? BackgroundColor
    {
        get;
        set;
    }

    public IFormFile? Media { get; set; }
}
