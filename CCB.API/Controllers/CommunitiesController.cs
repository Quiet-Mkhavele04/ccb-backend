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

private readonly IWebHostEnvironment
    _environment;

public CommunitiesController(
    CCBDbContext context,
    IWebHostEnvironment environment
)
{
    _context = context;
    _environment = environment;
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

// GET: /api/Communities/5/events
[HttpGet("{communityId:int}/events")]
public async Task<IActionResult> GetEvents(
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
                    "Join this community to view its events."
            }
        );
    }

    var events =
        await _context.CommunityEvents
            .AsNoTracking()
            .Where(communityEvent =>
                communityEvent.CommunityId ==
                communityId
            )
            .OrderBy(communityEvent =>
                communityEvent.EventDate == null
            )
            .ThenBy(communityEvent =>
                communityEvent.EventDate
            )
            .ThenBy(communityEvent =>
                communityEvent.Title
            )
            .Select(communityEvent => new
            {
                communityEventId =
                    communityEvent
                        .CommunityEventId,

                communityId =
                    communityEvent
                        .CommunityId,

                title =
                    communityEvent.Title,

                dateLabel =
                    communityEvent.DateLabel,

                eventDate =
                    communityEvent.EventDate,

                location =
                    communityEvent.Location,

                description =
                    communityEvent.Description
            })
            .ToListAsync();

    return Ok(new
    {
        events
    });
}

// POST: /api/Communities/5/events
[HttpPost("{communityId:int}/events")]
public async Task<IActionResult> CreateEvent(
    int communityId,
    [FromBody]
    CreateCommunityEventRequest request
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

    var title =
        request.Title?.Trim();

    var location =
        request.Location?.Trim();

    var description =
        request.Description?.Trim();

    if (string.IsNullOrWhiteSpace(title))
    {
        return BadRequest(new
        {
            message =
                "Enter an event title."
        });
    }

    if (title.Length > 200)
    {
        return BadRequest(new
        {
            message =
                "The event title cannot exceed 200 characters."
        });
    }

    if (!request.EventDate.HasValue)
    {
        return BadRequest(new
        {
            message =
                "Select an event date and time."
        });
    }

    if (
        !string.IsNullOrWhiteSpace(location) &&
        location.Length > 300
    )
    {
        return BadRequest(new
        {
            message =
                "The location cannot exceed 300 characters."
        });
    }

    if (
        !string.IsNullOrWhiteSpace(description) &&
        description.Length > 1000
    )
    {
        return BadRequest(new
        {
            message =
                "The description cannot exceed 1,000 characters."
        });
    }

    var community =
        await _context.Communities
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.CommunityId ==
                communityId
            );

    if (community == null)
    {
        return NotFound(new
        {
            message =
                "Community not found."
        });
    }

    var isAdmin =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value &&
                member.IsAdmin
            );

    if (!isAdmin)
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "Only a community administrator can create events."
            }
        );
    }

    var eventDate =
        request.EventDate.Value;

    var communityEvent =
        new CommunityEvent
        {
            CommunityId =
                communityId,

            Title =
                title,

            DateLabel =
                eventDate.ToString(
                    "ddd, dd MMM yyyy · HH:mm"
                ),

            EventDate =
                eventDate,

            Location =
                string.IsNullOrWhiteSpace(
                    location
                )
                    ? null
                    : location,

            Description =
                string.IsNullOrWhiteSpace(
                    description
                )
                    ? null
                    : description
        };

    _context.CommunityEvents.Add(
        communityEvent
    );

    await _context.SaveChangesAsync();

    return StatusCode(
        StatusCodes.Status201Created,
        new
        {
            message =
                "Community event created successfully.",

            communityEvent =
                new
                {
                    communityEventId =
                        communityEvent
                            .CommunityEventId,

                    communityId =
                        communityEvent
                            .CommunityId,

                    title =
                        communityEvent.Title,

                    dateLabel =
                        communityEvent.DateLabel,

                    eventDate =
                        communityEvent.EventDate,

                    location =
                        communityEvent.Location,

                    description =
                        communityEvent.Description
                }
        }
    );
}

// DELETE: /api/Communities/5/events/3
[HttpDelete(
    "{communityId:int}/events/{communityEventId:int}"
)]
public async Task<IActionResult> DeleteEvent(
    int communityId,
    int communityEventId
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

    var isAdmin =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value &&
                member.IsAdmin
            );

    if (!isAdmin)
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "Only a community administrator can delete events."
            }
        );
    }

    var communityEvent =
        await _context.CommunityEvents
            .FirstOrDefaultAsync(item =>
                item.CommunityEventId ==
                    communityEventId &&
                item.CommunityId ==
                    communityId
            );

    if (communityEvent == null)
    {
        return NotFound(new
        {
            message =
                "Community event not found."
        });
    }

    _context.CommunityEvents.Remove(
        communityEvent
    );

    await _context.SaveChangesAsync();

    return Ok(new
    {
        message =
            "Community event deleted successfully."
    });
}

// GET: /api/Communities/5/jobs
[HttpGet("{communityId:int}/jobs")]
public async Task<IActionResult> GetJobs(
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
                    "Join this community to view its opportunities."
            }
        );
    }

    var now = DateTime.UtcNow;

    var jobs =
        await _context.CommunityJobs
            .AsNoTracking()
            .Where(job =>
                job.CommunityId ==
                    communityId &&
                job.IsActive &&
                (
                    job.ExpiresAt == null ||
                    job.ExpiresAt >= now
                )
            )
            .OrderByDescending(job =>
                job.PostedAt
            )
            .Select(job => new
            {
                communityJobId =
                    job.CommunityJobId,

                communityId =
                    job.CommunityId,

                postedByProfileId =
                    job.PostedByProfileId,

                postedByName =
                    job.PostedByProfile != null
                        ? job.PostedByProfile.Name
                        : null,

                title =
                    job.Title,

                detail =
                    job.Detail,

                company =
                    job.Company,

                location =
                    job.Location,

                employmentType =
                    job.EmploymentType,

                applicationContact =
                    job.ApplicationContact,

                postedAt =
                    job.PostedAt,

                expiresAt =
                    job.ExpiresAt,

                isActive =
                    job.IsActive
            })
            .ToListAsync();

    return Ok(new
    {
        jobs
    });
}

// POST: /api/Communities/5/jobs
[HttpPost("{communityId:int}/jobs")]
public async Task<IActionResult> CreateJob(
    int communityId,
    [FromBody]
    CreateCommunityJobRequest request
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

    var title =
        request.Title?.Trim();

    var detail =
        request.Detail?.Trim();

    var company =
        request.Company?.Trim();

    var location =
        request.Location?.Trim();

    var employmentType =
        request.EmploymentType?.Trim();

    var applicationContact =
        request.ApplicationContact?.Trim();

    if (string.IsNullOrWhiteSpace(title))
    {
        return BadRequest(new
        {
            message =
                "Enter an opportunity title."
        });
    }

    if (title.Length > 200)
    {
        return BadRequest(new
        {
            message =
                "The title cannot exceed 200 characters."
        });
    }

    if (string.IsNullOrWhiteSpace(detail))
    {
        return BadRequest(new
        {
            message =
                "Enter the opportunity details."
        });
    }

    if (detail.Length > 2000)
    {
        return BadRequest(new
        {
            message =
                "The details cannot exceed 2,000 characters."
        });
    }

    if (company?.Length > 150)
    {
        return BadRequest(new
        {
            message =
                "The company name cannot exceed 150 characters."
        });
    }

    if (location?.Length > 200)
    {
        return BadRequest(new
        {
            message =
                "The location cannot exceed 200 characters."
        });
    }

    if (employmentType?.Length > 50)
    {
        return BadRequest(new
        {
            message =
                "The employment type cannot exceed 50 characters."
        });
    }

    if (applicationContact?.Length > 300)
    {
        return BadRequest(new
        {
            message =
                "The application contact cannot exceed 300 characters."
        });
    }

    if (
        request.ExpiresAt.HasValue &&
        request.ExpiresAt.Value <=
            DateTime.UtcNow
    )
    {
        return BadRequest(new
        {
            message =
                "The closing date must be in the future."
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

    var isAdmin =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value &&
                member.IsAdmin
            );

    if (!isAdmin)
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "Only a community administrator can post opportunities."
            }
        );
    }

    var postedAt = DateTime.UtcNow;

    var communityJob =
        new CommunityJob
        {
            CommunityId =
                communityId,

            PostedByProfileId =
                profileId.Value,

            Title =
                title,

            Detail =
                detail,

            Company =
                string.IsNullOrWhiteSpace(
                    company
                )
                    ? null
                    : company,

            Location =
                string.IsNullOrWhiteSpace(
                    location
                )
                    ? null
                    : location,

            EmploymentType =
                string.IsNullOrWhiteSpace(
                    employmentType
                )
                    ? null
                    : employmentType,

            ApplicationContact =
                string.IsNullOrWhiteSpace(
                    applicationContact
                )
                    ? null
                    : applicationContact,

            PostedAt =
                postedAt,

            ExpiresAt =
                request.ExpiresAt,

            IsActive =
                true
        };

    _context.CommunityJobs.Add(
        communityJob
    );

    await _context.SaveChangesAsync();

    var postedByName =
        await _context.Profiles
            .AsNoTracking()
            .Where(profile =>
                profile.ProfileId ==
                profileId.Value
            )
            .Select(profile =>
                profile.Name
            )
            .FirstOrDefaultAsync();

    return StatusCode(
        StatusCodes.Status201Created,
        new
        {
            message =
                "Opportunity posted successfully.",

            communityJob =
                new
                {
                    communityJobId =
                        communityJob
                            .CommunityJobId,

                    communityId =
                        communityJob
                            .CommunityId,

                    postedByProfileId =
                        communityJob
                            .PostedByProfileId,

                    postedByName,

                    title =
                        communityJob.Title,

                    detail =
                        communityJob.Detail,

                    company =
                        communityJob.Company,

                    location =
                        communityJob.Location,

                    employmentType =
                        communityJob
                            .EmploymentType,

                    applicationContact =
                        communityJob
                            .ApplicationContact,

                    postedAt =
                        communityJob.PostedAt,

                    expiresAt =
                        communityJob.ExpiresAt,

                    isActive =
                        communityJob.IsActive
                }
        }
    );
}

// DELETE: /api/Communities/5/jobs/3
[HttpDelete(
    "{communityId:int}/jobs/{communityJobId:int}"
)]
public async Task<IActionResult> DeleteJob(
    int communityId,
    int communityJobId
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

    var isAdmin =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value &&
                member.IsAdmin
            );

    if (!isAdmin)
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "Only a community administrator can remove opportunities."
            }
        );
    }

    var communityJob =
        await _context.CommunityJobs
            .FirstOrDefaultAsync(job =>
                job.CommunityJobId ==
                    communityJobId &&
                job.CommunityId ==
                    communityId
            );

    if (communityJob == null)
    {
        return NotFound(new
        {
            message =
                "Community opportunity not found."
        });
    }

    _context.CommunityJobs.Remove(
        communityJob
    );

    await _context.SaveChangesAsync();

    return Ok(new
    {
        message =
            "Community opportunity deleted successfully."
    });
}

// GET: /api/Communities/5/listings
[HttpGet("{communityId:int}/listings")]
public async Task<IActionResult> GetListings(
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
                    "Join this community to view its marketplace."
            }
        );
    }

    var rows =
        await _context.CommunityListings
            .AsNoTracking()
            .Where(listing =>
                listing.CommunityId ==
                communityId
            )
            .OrderByDescending(listing =>
                listing.IsAvailable
            )
            .ThenByDescending(listing =>
                listing.CreatedAt
            )
            .Select(listing => new
            {
                communityListingId =
                    listing.CommunityListingId,

                communityId =
                    listing.CommunityId,

                sellerProfileId =
                    listing.SellerProfileId,

                sellerName =
                    listing.SellerProfile != null
                        ? listing.SellerProfile.Name
                        : null,

                title =
                    listing.Title,

                price =
                    listing.Price,

                description =
                    listing.Description,

                category =
                    listing.Category,

                condition =
                    listing.Condition,

                location =
                    listing.Location,

                contact =
                    listing.Contact,

                storedImageUrl =
                    listing.ImageUrl,

                isAvailable =
                    listing.IsAvailable,

                createdAt =
                    listing.CreatedAt,

                updatedAt =
                    listing.UpdatedAt
            })
            .ToListAsync();

    var listings =
        rows.Select(listing => new
        {
            listing.communityListingId,
            listing.communityId,
            listing.sellerProfileId,
            listing.sellerName,
            listing.title,
            listing.price,
            listing.description,
            listing.category,
            listing.condition,
            listing.location,
            listing.contact,

            imageUrl =
                BuildPublicUrl(
                    listing.storedImageUrl
                ),

            listing.isAvailable,
            listing.createdAt,
            listing.updatedAt,

            isMine =
                listing.sellerProfileId ==
                profileId.Value
        });

    return Ok(new
    {
        listings
    });
}

// POST: /api/Communities/5/listings
[HttpPost("{communityId:int}/listings")]
public async Task<IActionResult> CreateListing(
    int communityId,
    [FromBody]
    CreateCommunityListingRequest request
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

    var title = request.Title?.Trim();
    var description =
        request.Description?.Trim();
    var category =
        request.Category?.Trim();
    var condition =
        request.Condition?.Trim();
    var location =
        request.Location?.Trim();
    var contact =
        request.Contact?.Trim();

    if (string.IsNullOrWhiteSpace(title))
    {
        return BadRequest(new
        {
            message =
                "Enter a listing title."
        });
    }

    if (title.Length > 200)
    {
        return BadRequest(new
        {
            message =
                "The title cannot exceed 200 characters."
        });
    }

    if (
        request.Price.HasValue &&
        request.Price.Value < 0
    )
    {
        return BadRequest(new
        {
            message =
                "The price cannot be negative."
        });
    }

    if (description?.Length > 1000)
    {
        return BadRequest(new
        {
            message =
                "The description cannot exceed 1,000 characters."
        });
    }

    if (category?.Length > 100)
    {
        return BadRequest(new
        {
            message =
                "The category cannot exceed 100 characters."
        });
    }

    if (condition?.Length > 50)
    {
        return BadRequest(new
        {
            message =
                "The condition cannot exceed 50 characters."
        });
    }

    if (location?.Length > 200)
    {
        return BadRequest(new
        {
            message =
                "The location cannot exceed 200 characters."
        });
    }

    if (string.IsNullOrWhiteSpace(contact))
    {
        return BadRequest(new
        {
            message =
                "Enter contact information for buyers."
        });
    }

    if (contact.Length > 300)
    {
        return BadRequest(new
        {
            message =
                "The contact information cannot exceed 300 characters."
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
                    "Join this community before creating a listing."
            }
        );
    }

    var sellerName =
        await _context.Profiles
            .AsNoTracking()
            .Where(profile =>
                profile.ProfileId ==
                profileId.Value
            )
            .Select(profile =>
                profile.Name
            )
            .FirstOrDefaultAsync();

    if (sellerName == null)
    {
        return NotFound(new
        {
            message =
                "Profile not found."
        });
    }

    var createdAt = DateTime.UtcNow;

    var listing =
        new CommunityListing
        {
            CommunityId =
                communityId,

            SellerProfileId =
                profileId.Value,

            Title =
                title,

            Price =
                request.Price,

            Description =
                string.IsNullOrWhiteSpace(
                    description
                )
                    ? null
                    : description,

            Category =
                string.IsNullOrWhiteSpace(
                    category
                )
                    ? null
                    : category,

            Condition =
                string.IsNullOrWhiteSpace(
                    condition
                )
                    ? null
                    : condition,

            Location =
                string.IsNullOrWhiteSpace(
                    location
                )
                    ? null
                    : location,

            Contact =
                contact,

            ImageUrl =
                null,

            IsAvailable =
                true,

            CreatedAt =
                createdAt,

            UpdatedAt =
                null
        };

    _context.CommunityListings.Add(
        listing
    );

    await _context.SaveChangesAsync();

    return StatusCode(
        StatusCodes.Status201Created,
        new
        {
            message =
                "Marketplace listing created successfully.",

            communityListing =
                new
                {
                    communityListingId =
                        listing
                            .CommunityListingId,

                    communityId =
                        listing.CommunityId,

                    sellerProfileId =
                        listing
                            .SellerProfileId,

                    sellerName,

                    title =
                        listing.Title,

                    price =
                        listing.Price,

                    description =
                        listing.Description,

                    category =
                        listing.Category,

                    condition =
                        listing.Condition,

                    location =
                        listing.Location,

                    contact =
                        listing.Contact,

                    imageUrl =
                        (string?)null,

                    isAvailable =
                        listing.IsAvailable,

                    createdAt =
                        listing.CreatedAt,

                    updatedAt =
                        listing.UpdatedAt,

                    isMine = true
                }
        }
    );
}

// POST: /api/Communities/5/listings/3/image
[HttpPost(
    "{communityId:int}/listings/{communityListingId:int}/image"
)]
[Consumes("multipart/form-data")]
public async Task<IActionResult> UploadListingImage(
    int communityId,
    int communityListingId,
    IFormFile image
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

    if (image == null || image.Length == 0)
    {
        return BadRequest(new
        {
            message =
                "Choose an image to upload."
        });
    }

    if (image.Length > 5 * 1024 * 1024)
    {
        return BadRequest(new
        {
            message =
                "The image must be smaller than 5 MB."
        });
    }

    var extension =
        Path.GetExtension(
            image.FileName
        ).ToLowerInvariant();

    var allowedExtensions =
        new[]
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

    if (!allowedExtensions.Contains(extension))
    {
        return BadRequest(new
        {
            message =
                "Only JPG, PNG and WEBP images are supported."
        });
    }

    var listing =
        await _context.CommunityListings
            .FirstOrDefaultAsync(item =>
                item.CommunityListingId ==
                    communityListingId &&
                item.CommunityId ==
                    communityId
            );

    if (listing == null)
    {
        return NotFound(new
        {
            message =
                "Marketplace listing not found."
        });
    }

    var isAdmin =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value &&
                member.IsAdmin
            );

    if (
        listing.SellerProfileId !=
            profileId.Value &&
        !isAdmin
    )
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "You cannot change another member's listing."
            }
        );
    }

    DeleteStoredListingImage(
        listing.ImageUrl
    );

    var webRoot =
        _environment.WebRootPath;

    if (string.IsNullOrWhiteSpace(webRoot))
    {
        webRoot = Path.Combine(
            _environment.ContentRootPath,
            "wwwroot"
        );
    }

    var directory =
        Path.Combine(
            webRoot,
            "uploads",
            "community-listings"
        );

    Directory.CreateDirectory(directory);

    var fileName =
        $"listing-{communityListingId}-{Guid.NewGuid():N}{extension}";

    var filePath =
        Path.Combine(
            directory,
            fileName
        );

    await using (
        var stream =
            System.IO.File.Create(filePath)
    )
    {
        await image.CopyToAsync(stream);
    }

    listing.ImageUrl =
        $"/uploads/community-listings/{fileName}";

    listing.UpdatedAt =
        DateTime.UtcNow;

    await _context.SaveChangesAsync();

    return Ok(new
    {
        message =
            "Listing image uploaded successfully.",

        imageUrl =
            BuildPublicUrl(
                listing.ImageUrl
            )
    });
}

// PATCH: /api/Communities/5/listings/3/availability
[HttpPatch(
    "{communityId:int}/listings/{communityListingId:int}/availability"
)]
public async Task<IActionResult> UpdateListingAvailability(
    int communityId,
    int communityListingId,
    [FromBody]
    UpdateListingAvailabilityRequest request
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

    var listing =
        await _context.CommunityListings
            .FirstOrDefaultAsync(item =>
                item.CommunityListingId ==
                    communityListingId &&
                item.CommunityId ==
                    communityId
            );

    if (listing == null)
    {
        return NotFound(new
        {
            message =
                "Marketplace listing not found."
        });
    }

    var isAdmin =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value &&
                member.IsAdmin
            );

    if (
        listing.SellerProfileId !=
            profileId.Value &&
        !isAdmin
    )
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "You cannot update another member's listing."
            }
        );
    }

    listing.IsAvailable =
        request.IsAvailable;

    listing.UpdatedAt =
        DateTime.UtcNow;

    await _context.SaveChangesAsync();

    return Ok(new
    {
        message =
            request.IsAvailable
                ? "Listing marked as available."
                : "Listing marked as sold.",

        isAvailable =
            listing.IsAvailable
    });
}

// DELETE: /api/Communities/5/listings/3
[HttpDelete(
    "{communityId:int}/listings/{communityListingId:int}"
)]
public async Task<IActionResult> DeleteListing(
    int communityId,
    int communityListingId
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

    var listing =
        await _context.CommunityListings
            .FirstOrDefaultAsync(item =>
                item.CommunityListingId ==
                    communityListingId &&
                item.CommunityId ==
                    communityId
            );

    if (listing == null)
    {
        return NotFound(new
        {
            message =
                "Marketplace listing not found."
        });
    }

    var isAdmin =
        await _context.CommunityMembers
            .AsNoTracking()
            .AnyAsync(member =>
                member.CommunityId ==
                    communityId &&
                member.ProfileId ==
                    profileId.Value &&
                member.IsAdmin
            );

    if (
        listing.SellerProfileId !=
            profileId.Value &&
        !isAdmin
    )
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                message =
                    "You cannot delete another member's listing."
            }
        );
    }

    DeleteStoredListingImage(
        listing.ImageUrl
    );

    _context.CommunityListings.Remove(
        listing
    );

    await _context.SaveChangesAsync();

    return Ok(new
    {
        message =
            "Marketplace listing deleted successfully."
    });
}
    private string? BuildPublicUrl(
        string? storedUrl
    )
    {
        if (string.IsNullOrWhiteSpace(storedUrl))
        {
            return null;
        }

        if (
            storedUrl.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase
            ) ||
            storedUrl.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return storedUrl;
        }

        return
            $"{Request.Scheme}://{Request.Host}{storedUrl}";
    }

    private void DeleteStoredListingImage(
        string? storedUrl
    )
    {
        if (
            string.IsNullOrWhiteSpace(storedUrl) ||
            !storedUrl.StartsWith(
                "/uploads/community-listings/",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        var webRoot =
            _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(
                _environment.ContentRootPath,
                "wwwroot"
            );
        }

        var fileName =
            Path.GetFileName(storedUrl);

        var filePath =
            Path.Combine(
                webRoot,
                "uploads",
                "community-listings",
                fileName
            );

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
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

public sealed class CreateCommunityEventRequest
{
    public string Title {
        get;
        set;
    } = string.Empty;

    public DateTime? EventDate {
        get;
        set;
    }

    public string? Location {
        get;
        set;
    }

    public string? Description {
        get;
        set;
    }
}

public sealed class CreateCommunityJobRequest
{
    public string Title {
        get;
        set;
    } = string.Empty;

    public string Detail {
        get;
        set;
    } = string.Empty;

    public string? Company {
        get;
        set;
    }

    public string? Location {
        get;
        set;
    }

    public string? EmploymentType {
        get;
        set;
    }

    public string? ApplicationContact {
        get;
        set;
    }

    public DateTime? ExpiresAt {
        get;
        set;
    }
}

public sealed class CreateCommunityListingRequest
{
    public string Title {
        get;
        set;
    } = string.Empty;

    public decimal? Price {
        get;
        set;
    }

    public string? Description {
        get;
        set;
    }

    public string? Category {
        get;
        set;
    }

    public string? Condition {
        get;
        set;
    }

    public string? Location {
        get;
        set;
    }

    public string? Contact {
        get;
        set;
    }
}

public sealed class UpdateListingAvailabilityRequest
{
    public bool IsAvailable {
        get;
        set;
    }
}
