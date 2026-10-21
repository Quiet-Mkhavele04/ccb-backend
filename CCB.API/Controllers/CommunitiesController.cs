using System.Security.Claims;
using System.Text.RegularExpressions;
using CCB.API.Data;
using CCB.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommunitiesController : ControllerBase
{
    private readonly CCBDbContext _context;

    public CommunitiesController(
        CCBDbContext context
    )
    {
        _context = context;
    }

    // POST: /api/Communities
    [HttpPost]
    public async Task<IActionResult> CreateCommunity(
        [FromBody] CreateCommunityRequest request
    )
    {
        var profileId = GetProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "Your login session is invalid."
            });
        }

        var name = request.Name?.Trim();
        var category = request.Category?.Trim();
        var description = request.Description?.Trim();
        var bannerColor = request.BannerColor?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                message = "Enter a community name."
            });
        }

        if (name.Length > 150)
        {
            return BadRequest(new
            {
                message = "The community name cannot exceed 150 characters."
            });
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return BadRequest(new
            {
                message = "Select a community category."
            });
        }

        if (category.Length > 100)
        {
            return BadRequest(new
            {
                message = "The category cannot exceed 100 characters."
            });
        }

        if (description?.Length > 2000)
        {
            return BadRequest(new
            {
                message = "The description cannot exceed 2,000 characters."
            });
        }

        if (
            !string.IsNullOrWhiteSpace(bannerColor) &&
            !Regex.IsMatch(
                bannerColor,
                "^#[0-9A-Fa-f]{6}$"
            )
        )
        {
            return BadRequest(new
            {
                message = "Select a valid community colour."
            });
        }

        var profileExists = await _context.Profiles
            .AsNoTracking()
            .AnyAsync(profile =>
                profile.ProfileId == profileId.Value
            );

        if (!profileExists)
        {
            return NotFound(new
            {
                message = "Profile not found."
            });
        }

        var nameExists = await _context.Communities
            .AsNoTracking()
            .AnyAsync(community =>
                community.Name == name
            );

        if (nameExists)
        {
            return Conflict(new
            {
                message = "A community with this name already exists."
            });
        }

        var now = DateTime.UtcNow;

        var community = new Community
        {
            Name = name,
            Category = category,
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description,
            BannerColor = string.IsNullOrWhiteSpace(bannerColor)
                ? "#1A6B4A"
                : bannerColor,
            MemberCount = 1,
            Joined = false,
            CreatedAt = now
        };

        community.CommunityMembers.Add(
            new CommunityMember
            {
                ProfileId = profileId.Value,
                JoinedAt = now,
                IsAdmin = true
            }
        );

        _context.Communities.Add(community);
        await _context.SaveChangesAsync();

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                message = "Community created successfully.",
                community = new
                {
                    communityId = community.CommunityId,
                    name = community.Name,
                    category = community.Category,
                    description = community.Description,
                    bannerColor = community.BannerColor,
                    memberCount = 1,
                    joined = true,
                    isAdmin = true,
                    createdAt = community.CreatedAt
                }
            }
        );
    }

    // GET: /api/Communities
    // Optional: ?search=sport&category=Sports&joinedOnly=true
    [HttpGet]
    public async Task<IActionResult> GetCommunities(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] bool joinedOnly = false
    )
    {
        var profileId = GetProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "Your login session is invalid."
            });
        }

        var query = _context.Communities
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchText = search.Trim();

            query = query.Where(community =>
                community.Name.Contains(searchText) ||
                (
                    community.Description != null &&
                    community.Description.Contains(searchText)
                )
            );
        }

        if (
            !string.IsNullOrWhiteSpace(category) &&
            !string.Equals(
                category.Trim(),
                "All",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var categoryText = category.Trim();

            query = query.Where(community =>
                community.Category == categoryText
            );
        }

        if (joinedOnly)
        {
            query = query.Where(community =>
                community.CommunityMembers.Any(member =>
                    member.ProfileId == profileId.Value
                )
            );
        }

        var communities = await query
            .OrderByDescending(community =>
                community.CommunityMembers.Any(member =>
                    member.ProfileId == profileId.Value
                )
            )
            .ThenByDescending(community =>
                community.CommunityMembers.Count()
            )
            .ThenBy(community => community.Name)
            .Select(community => new
            {
                communityId = community.CommunityId,
                name = community.Name,
                category = community.Category,
                description = community.Description,
                bannerColor = community.BannerColor,
                memberCount = community.CommunityMembers.Count(),
                joined = community.CommunityMembers.Any(member =>
                    member.ProfileId == profileId.Value
                ),
                isAdmin = community.CommunityMembers.Any(member =>
                    member.ProfileId == profileId.Value &&
                    member.IsAdmin
                ),
                createdAt = community.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            communities
        });
    }

    // GET: /api/Communities/5
    [HttpGet("{communityId:int}")]
    public async Task<IActionResult> GetCommunity(
        int communityId
    )
    {
        var profileId = GetProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "Your login session is invalid."
            });
        }

        var community = await _context.Communities
            .AsNoTracking()
            .Where(item =>
                item.CommunityId == communityId
            )
            .Select(item => new
            {
                communityId = item.CommunityId,
                name = item.Name,
                category = item.Category,
                description = item.Description,
                bannerColor = item.BannerColor,
                memberCount = item.CommunityMembers.Count(),
                joined = item.CommunityMembers.Any(member =>
                    member.ProfileId == profileId.Value
                ),
                isAdmin = item.CommunityMembers.Any(member =>
                    member.ProfileId == profileId.Value &&
                    member.IsAdmin
                ),
                createdAt = item.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (community == null)
        {
            return NotFound(new
            {
                message = "Community not found."
            });
        }

        return Ok(new
        {
            community
        });
    }

    // POST: /api/Communities/5/join
    [HttpPost("{communityId:int}/join")]
    public async Task<IActionResult> JoinCommunity(
        int communityId
    )
    {
        var profileId = GetProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "Your login session is invalid."
            });
        }

        var community = await _context.Communities
            .FirstOrDefaultAsync(item =>
                item.CommunityId == communityId
            );

        if (community == null)
        {
            return NotFound(new
            {
                message = "Community not found."
            });
        }

        var alreadyJoined = await _context.CommunityMembers
            .AnyAsync(member =>
                member.CommunityId == communityId &&
                member.ProfileId == profileId.Value
            );

        if (alreadyJoined)
        {
            return Ok(new
            {
                message = "You have already joined this community."
            });
        }

        _context.CommunityMembers.Add(
            new CommunityMember
            {
                CommunityId = communityId,
                ProfileId = profileId.Value,
                JoinedAt = DateTime.UtcNow,
                IsAdmin = false
            }
        );

        community.MemberCount += 1;

        await _context.SaveChangesAsync();

        var memberCount = await _context.CommunityMembers
            .CountAsync(member =>
                member.CommunityId == communityId
            );

        return Ok(new
        {
            message = "Community joined successfully.",
            communityId,
            joined = true,
            memberCount
        });
    }

    // DELETE: /api/Communities/5/leave
    [HttpDelete("{communityId:int}/leave")]
    public async Task<IActionResult> LeaveCommunity(
        int communityId
    )
    {
        var profileId = GetProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "Your login session is invalid."
            });
        }

        var community = await _context.Communities
            .FirstOrDefaultAsync(item =>
                item.CommunityId == communityId
            );

        if (community == null)
        {
            return NotFound(new
            {
                message = "Community not found."
            });
        }

        var membership = await _context.CommunityMembers
            .FirstOrDefaultAsync(member =>
                member.CommunityId == communityId &&
                member.ProfileId == profileId.Value
            );

        if (membership == null)
        {
            return Ok(new
            {
                message = "You are not a member of this community."
            });
        }

        if (membership.IsAdmin)
        {
            var otherAdminExists =
                await _context.CommunityMembers
                    .AnyAsync(member =>
                        member.CommunityId == communityId &&
                        member.ProfileId != profileId.Value &&
                        member.IsAdmin
                    );

            if (!otherAdminExists)
            {
                return Conflict(new
                {
                    message =
                        "Add another administrator before leaving this community."
                });
            }
        }

        _context.CommunityMembers.Remove(membership);

        community.MemberCount = Math.Max(
            0,
            community.MemberCount - 1
        );

        await _context.SaveChangesAsync();

        var memberCount = await _context.CommunityMembers
            .CountAsync(member =>
                member.CommunityId == communityId
            );

        return Ok(new
        {
            message = "You left the community successfully.",
            communityId,
            joined = false,
            memberCount
        });
    }
    // GET: /api/Communities/5/messages
[HttpGet("{communityId:int}/messages")]
public async Task<IActionResult> GetMessages(
    int communityId
)
{
    var profileId = GetProfileId();

    if (profileId == null)
    {
        return Unauthorized(new
        {
            message =
                "Your login session is invalid."
        });
    }

    var communityExists =
        await _context.Communities
            .AsNoTracking()
            .AnyAsync(community =>
                community.CommunityId ==
                communityId
            );

    if (!communityExists)
    {
        return NotFound(new
        {
            message =
                "Community not found."
        });
    }

    var isMember =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value
            );

    if (!isMember)
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "Join this community to view its messages."
            }
        );
    }

    // Load the newest 100 messages, then
    // arrange them from oldest to newest.
    var messages =
        await _context.CommunityMessages
            .AsNoTracking()
            .Where(message =>
                message.CommunityId ==
                communityId
            )
            .OrderByDescending(message =>
                message.SentAt
            )
            .Take(100)
            .Select(message => new
            {
                communityMessageId =
                    message.CommunityMessageId,

                communityId =
                    message.CommunityId,

                senderProfileId =
                    message.SenderProfileId,

                senderName =
                    message.SenderProfileId != null &&
                    message.SenderProfile != null
                        ? message.SenderProfile.Name
                        : message.SenderName,

                avatarColor =
                    message.SenderProfileId != null &&
                    message.SenderProfile != null
                        ? message.SenderProfile.AvatarColor
                        : message.AvatarColor,

                profilePhotoUrl =
                    message.SenderProfileId != null &&
                    message.SenderProfile != null
                        ? message.SenderProfile
                            .ProfilePhotoUrl
                        : null,

                messageText =
                    message.MessageText,

                sentAt =
                    message.SentAt,

                isMe =
                    message.SenderProfileId ==
                    profileId.Value
            })
            .ToListAsync();

    messages.Reverse();

    return Ok(new
    {
        messages
    });
}

// POST: /api/Communities/5/messages
[HttpPost("{communityId:int}/messages")]
public async Task<IActionResult> SendMessage(
    int communityId,
    [FromBody]
    SendCommunityMessageRequest request
)
{
    var profileId = GetProfileId();

    if (profileId == null)
    {
        return Unauthorized(new
        {
            message =
                "Your login session is invalid."
        });
    }

    var messageText =
        request.MessageText?.Trim();

    if (string.IsNullOrWhiteSpace(
        messageText
    ))
    {
        return BadRequest(new
        {
            message =
                "Enter a message."
        });
    }

    if (messageText.Length > 2000)
    {
        return BadRequest(new
        {
            message =
                "A message cannot exceed 2,000 characters."
        });
    }

    var communityExists =
        await _context.Communities
            .AsNoTracking()
            .AnyAsync(community =>
                community.CommunityId ==
                communityId
            );

    if (!communityExists)
    {
        return NotFound(new
        {
            message =
                "Community not found."
        });
    }

    var isMember =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value
            );

    if (!isMember)
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "Join this community before sending messages."
            }
        );
    }

    var profile =
        await _context.Profiles
            .AsNoTracking()
            .Where(item =>
                item.ProfileId ==
                profileId.Value
            )
            .Select(item => new
            {
                item.ProfileId,
                item.Name,
                item.AvatarColor,
                item.ProfilePhotoUrl
            })
            .FirstOrDefaultAsync();

    if (profile == null)
    {
        return NotFound(new
        {
            message =
                "Profile not found."
        });
    }

    var sentAt = DateTime.UtcNow;

    var communityMessage =
        new CommunityMessage
        {
            CommunityId =
                communityId,

            SenderProfileId =
                profile.ProfileId,

            SenderName =
                string.IsNullOrWhiteSpace(
                    profile.Name
                )
                    ? "Member"
                    : profile.Name,

            AvatarColor =
                profile.AvatarColor,

            MessageText =
                messageText,

            SentAt =
                sentAt,

            // This legacy database value is not
            // trusted. isMe is calculated for
            // each logged-in user in responses.
            IsMe = false
        };

    _context.CommunityMessages.Add(
        communityMessage
    );

    await _context.SaveChangesAsync();

    return StatusCode(
        StatusCodes.Status201Created,
        new
        {
            message =
                "Message sent successfully.",

            communityMessage =
                new
                {
                    communityMessageId =
                        communityMessage
                            .CommunityMessageId,

                    communityId =
                        communityMessage
                            .CommunityId,

                    senderProfileId =
                        profile.ProfileId,

                    senderName =
                        communityMessage
                            .SenderName,

                    avatarColor =
                        profile.AvatarColor,

                    profilePhotoUrl =
                        profile.ProfilePhotoUrl,

                    messageText =
                        communityMessage
                            .MessageText,

                    sentAt =
                        communityMessage
                            .SentAt,

                    isMe = true
                }
        }
    );
}

    private int? GetProfileId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            )
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("profileId")
            ?? User.FindFirstValue("ProfileId");

        return int.TryParse(
            value,
            out var profileId
        )
            ? profileId
            : null;
    }
}

public sealed class CreateCommunityRequest
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? BannerColor { get; set; }
}

public sealed class SendCommunityMessageRequest
{
    public string MessageText {
        get;
        set;
    } = string.Empty;
}