using System.Security.Claims;
using CCB.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly CCBDbContext _context;

    public ContactsController(
        CCBDbContext context
    )
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyContacts()
    {
        var profileIdValue =
            User.FindFirstValue("profileId");

        if (!int.TryParse(
            profileIdValue,
            out var currentProfileId
        ))
        {
            return Unauthorized(new
            {
                message =
                    "The access token does not contain a valid profile."
            });
        }

        var contactRows =
            await _context.ProfileContacts
                .AsNoTracking()
                .Where(profileContact =>
                    profileContact.ProfileId ==
                    currentProfileId
                )
                .Select(profileContact => new
                {
                    contactId =
                        profileContact.Contact
                            .ContactId,

                    profileId =
                        profileContact.Contact
                            .ProfileId,

                    name =
                        profileContact.Contact
                            .Name,

                    avatarColor =
                        profileContact.Contact
                            .AvatarColor,

                    language =
                        profileContact.Contact
                            .Language,

                    isOnline =
                        profileContact.Contact
                            .IsOnline,

                    pinned =
                        profileContact.Contact
                            .Pinned,

                    addedAt =
                        profileContact.AddedAt
                })
                .ToListAsync();

        var otherProfileIds =
            contactRows
                .Where(contact =>
                    contact.profileId.HasValue
                )
                .Select(contact =>
                    contact.profileId!.Value
                )
                .Distinct()
                .ToList();

        /*
         * Load the profile photos belonging to
         * each contact's linked CCB profile.
         */
        var profilePhotos =
            await _context.Profiles
                .AsNoTracking()
                .Where(profile =>
                    otherProfileIds.Contains(
                        profile.ProfileId
                    )
                )
                .Select(profile => new
                {
                    profile.ProfileId,
                    profile.ProfilePhotoUrl
                })
                .ToDictionaryAsync(
                    profile =>
                        profile.ProfileId,
                    profile =>
                        profile.ProfilePhotoUrl
                );

        /*
         * Load messages between the current user
         * and all their contacts.
         */
        var conversationMessages =
            await _context.Messages
                .AsNoTracking()
                .Where(message =>
                    !message.IsDeleted &&
                    message.SenderProfileId
                        .HasValue &&
                    message.RecipientProfileId
                        .HasValue &&
                    (
                        (
                            message.SenderProfileId ==
                                currentProfileId &&
                            otherProfileIds.Contains(
                                message
                                    .RecipientProfileId
                                    .Value
                            )
                        )
                        ||
                        (
                            message.RecipientProfileId ==
                                currentProfileId &&
                            otherProfileIds.Contains(
                                message
                                    .SenderProfileId
                                    .Value
                            )
                        )
                    )
                )
                .OrderByDescending(message =>
                    message.SentAt
                )
                .Select(message => new
                {
                    message.MessageId,

                    message.SenderProfileId,

                    message.RecipientProfileId,

                    message.MessageText,

                    message.SentAt,

                    message.ReadAt,

                    message.Translated
                })
                .ToListAsync();

        var contacts =
            contactRows
                .Select(contact =>
                {
                    var latestMessage =
                        contact.profileId.HasValue
                            ? conversationMessages
                                .FirstOrDefault(
                                    message =>
                                        (
                                            message
                                                .SenderProfileId ==
                                                currentProfileId &&
                                            message
                                                .RecipientProfileId ==
                                                contact.profileId
                                        )
                                        ||
                                        (
                                            message
                                                .SenderProfileId ==
                                                contact.profileId &&
                                            message
                                                .RecipientProfileId ==
                                                currentProfileId
                                        )
                                )
                            : null;

                    var unreadCount =
                        contact.profileId.HasValue
                            ? conversationMessages.Count(
                                message =>
                                    message
                                        .SenderProfileId ==
                                        contact.profileId &&
                                    message
                                        .RecipientProfileId ==
                                        currentProfileId &&
                                    message.ReadAt == null
                            )
                            : 0;

                    return new
                    {
                        contact.contactId,

                        contact.profileId,

                        contact.name,

                        contact.avatarColor,

                        profilePhotoUrl =
                            contact.profileId.HasValue &&
                            profilePhotos.TryGetValue(
                                contact.profileId.Value,
                                out var photoUrl
                            )
                                ? photoUrl
                                : null,

                        contact.language,

                        contact.isOnline,

                        contact.pinned,

                        unreadCount,

                        contact.addedAt,

                        lastMessageText =
                            latestMessage
                                ?.MessageText,

                        lastMessageSentAt =
                            latestMessage
                                ?.SentAt,

                        lastMessageIsMine =
                            latestMessage != null &&
                            latestMessage
                                .SenderProfileId ==
                                currentProfileId,

                        lastMessageTranslated =
                            latestMessage
                                ?.Translated ??
                            false
                    };
                })
                .OrderByDescending(contact =>
                    contact.pinned
                )
                .ThenByDescending(contact =>
                    contact.lastMessageSentAt
                )
                .ThenBy(contact =>
                    contact.name
                )
                .ToList();

        return Ok(new
        {
            contacts
        });
    }
}