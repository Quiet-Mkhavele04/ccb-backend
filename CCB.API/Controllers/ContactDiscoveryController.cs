using System.Security.Claims;
using CCB.API.Data;
using CCB.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/Contacts")]
[Authorize]
public class ContactDiscoveryController : ControllerBase
{
    private readonly CCBDbContext _context;

    public ContactDiscoveryController(
        CCBDbContext context
    )
    {
        _context = context;
    }

    // GET:
    // api/Contacts/search?query=jasi
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] string? query
    )
    {
        var currentProfileId =
            GetCurrentProfileId();

        if (currentProfileId == null)
        {
            return Unauthorized(new
            {
                message =
                    "A valid access token is required."
            });
        }

        var searchText =
            query?.Trim();

        if (string.IsNullOrWhiteSpace(
            searchText
        ))
        {
            return Ok(new
            {
                users = Array.Empty<object>()
            });
        }

        if (searchText.Length < 2)
        {
            return BadRequest(new
            {
                message =
                    "Enter at least two characters."
            });
        }

        var addedProfileIds =
            await _context.ProfileContacts
                .AsNoTracking()
                .Where(profileContact =>
                    profileContact.ProfileId ==
                        currentProfileId.Value &&
                    profileContact.Contact
                        .ProfileId.HasValue
                )
                .Select(profileContact =>
                    profileContact.Contact
                        .ProfileId!.Value
                )
                .ToListAsync();

        var profiles =
            await _context.Profiles
                .AsNoTracking()
                .Where(profile =>
                    profile.ProfileId !=
                        currentProfileId.Value &&
                    (
                        (
                            profile.Name != null &&
                            profile.Name.Contains(
                                searchText
                            )
                        )
                        ||
                        (
                            profile.Username != null &&
                            profile.Username.Contains(
                                searchText
                            )
                        )
                        ||
                        (
                            profile.Phone != null &&
                            profile.Phone.Contains(
                                searchText
                            )
                        )
                    )
                )
                .OrderBy(profile =>
                    profile.Name
                )
                .Take(20)
                .Select(profile => new
                {
                    profileId =
                        profile.ProfileId,

                    name =
                        profile.Name,

                    username =
                        profile.Username,

                    phone =
                        profile.Phone,

                    avatarColor =
                        profile.AvatarColor,

                    language =
                        profile.Language
                })
                .ToListAsync();

        var users =
            profiles.Select(profile => new
            {
                profile.profileId,

                profile.name,

                profile.username,

                profile.phone,

                profile.avatarColor,

                profile.language,

                isAdded =
                    addedProfileIds.Contains(
                        profile.profileId
                    )
            });

        return Ok(new
        {
            users
        });
    }

    // POST: api/Contacts/add
    [HttpPost("add")]
    public async Task<IActionResult> AddContact(
        AddContactRequest request
    )
    {
        var currentProfileId =
            GetCurrentProfileId();

        if (currentProfileId == null)
        {
            return Unauthorized(new
            {
                message =
                    "A valid access token is required."
            });
        }

        if (request.ProfileId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Select a valid CCB user."
            });
        }

        if (request.ProfileId ==
            currentProfileId.Value)
        {
            return BadRequest(new
            {
                message =
                    "You cannot add yourself as a contact."
            });
        }

        var targetProfile =
            await _context.Profiles
                .FirstOrDefaultAsync(
                    profile =>
                        profile.ProfileId ==
                        request.ProfileId
                );

        if (targetProfile == null)
        {
            return NotFound(new
            {
                message =
                    "The selected CCB user was not found."
            });
        }

        var contact =
            await _context.Contacts
                .FirstOrDefaultAsync(
                    contact =>
                        contact.ProfileId ==
                        targetProfile.ProfileId
                );

        if (contact != null)
        {
            var alreadyAdded =
                await _context.ProfileContacts
                    .AnyAsync(
                        profileContact =>
                            profileContact.ProfileId ==
                                currentProfileId.Value &&
                            profileContact.ContactId ==
                                contact.ContactId
                    );

            if (alreadyAdded)
            {
                return Conflict(new
                {
                    message =
                        "This user is already in your contacts."
                });
            }
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        try
        {
            if (contact == null)
            {
                contact = new Contact
                {
                    ProfileId =
                        targetProfile.ProfileId,

                    Name =
                        targetProfile.Name ??
                        targetProfile.Username ??
                        "CCB user",

                    AvatarColor =
                        targetProfile.AvatarColor ??
                        "#0D5740",

                    Language =
                        targetProfile.Language ??
                        "English",

                    IsOnline = false,

                    Pinned = false,

                    UnreadCount = 0,

                    CreatedAt =
                        DateTime.UtcNow
                };

                _context.Contacts.Add(
                    contact
                );

                await _context
                    .SaveChangesAsync();
            }

            var profileContact =
                new ProfileContact
                {
                    ProfileId =
                        currentProfileId.Value,

                    ContactId =
                        contact.ContactId,

                    AddedAt =
                        DateTime.UtcNow
                };

            _context.ProfileContacts.Add(
                profileContact
            );

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                message =
                    "Contact added successfully.",

                contact = new
                {
                    contactId =
                        contact.ContactId,

                    profileId =
                        contact.ProfileId,

                    name =
                        contact.Name,

                    avatarColor =
                        contact.AvatarColor,

                    language =
                        contact.Language,

                    isOnline =
                        contact.IsOnline,

                    pinned =
                        contact.Pinned,

                    unreadCount =
                        contact.UnreadCount,

                    addedAt =
                        profileContact.AddedAt,

                    lastMessageText =
                        (string?)null,

                    lastMessageSentAt =
                        (DateTime?)null,

                    lastMessageIsMine =
                        false,

                    lastMessageTranslated =
                        false
                }
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private int? GetCurrentProfileId()
    {
        var profileIdValue =
            User.FindFirstValue(
                "profileId"
            );

        return int.TryParse(
            profileIdValue,
            out var profileId
        )
            ? profileId
            : null;
    }
}

public class AddContactRequest
{
    public int ProfileId { get; set; }
}