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
public class MessagesController : ControllerBase
{
    private readonly CCBDbContext _context;
    private readonly IHubContext<ChatHub> _chatHub;

    public MessagesController(
        CCBDbContext context,
        IHubContext<ChatHub> chatHub
    )
    {
        _context = context;
        _chatHub = chatHub;
    }

    // GET: api/Messages/contact/3
    [HttpGet("contact/{contactId:int}")]
    public async Task<IActionResult> GetConversation(int contactId)
    {
        var profileId = GetCurrentProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "A valid access token is required."
            });
        }

        var currentProfileId = profileId.Value;

        var contact = await _context.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(contact =>
                contact.ContactId == contactId
            );

        if (contact == null)
        {
            return NotFound(new
            {
                message = "The contact was not found."
            });
        }

        if (contact.ProfileId == null)
        {
            return BadRequest(new
            {
                message = "This contact does not have a CCB profile."
            });
        }

        var isMyContact = await _context.ProfileContacts
            .AnyAsync(profileContact =>
                profileContact.ProfileId == currentProfileId &&
                profileContact.ContactId == contactId
            );

        if (!isMyContact)
        {
            return Forbid();
        }

        var otherProfileId = contact.ProfileId.Value;

        var unreadMessages = await _context.Messages
            .Where(message =>
                !message.IsDeleted &&
                message.SenderProfileId == otherProfileId &&
                message.RecipientProfileId == currentProfileId &&
                message.ReadAt == null
            )
            .ToListAsync();

        if (unreadMessages.Count > 0)
        {
            var readTime = DateTime.UtcNow;

            foreach (var unreadMessage in unreadMessages)
            {
                unreadMessage.ReadAt = readTime;
            }

            await _context.SaveChangesAsync();
        }

        var messages = await _context.Messages
            .AsNoTracking()
            .Where(message =>
                !message.IsDeleted &&
                (
                    (
                        message.SenderProfileId == currentProfileId &&
                        message.RecipientProfileId == otherProfileId
                    )
                    ||
                    (
                        message.SenderProfileId == otherProfileId &&
                        message.RecipientProfileId == currentProfileId
                    )
                )
            )
            .OrderBy(message => message.SentAt)
            .Select(message => new
            {
                messageId = message.MessageId,
                senderProfileId = message.SenderProfileId,
                recipientProfileId = message.RecipientProfileId,
                messageText = message.MessageText,
                isMine = message.SenderProfileId == currentProfileId,
                translated = message.Translated,
                sentAt = message.SentAt,
                readAt = message.ReadAt,
                editedAt = message.EditedAt
            })
            .ToListAsync();

        return Ok(new
        {
            contact = new
            {
                contactId = contact.ContactId,
                profileId = contact.ProfileId,
                name = contact.Name,
                avatarColor = contact.AvatarColor,
                isOnline = contact.IsOnline,
                language = contact.Language
            },
            messages
        });
    }

    // POST: api/Messages
    [HttpPost]
    public async Task<IActionResult> SendMessage(
        SendMessageRequest request
    )
    {
        var profileId = GetCurrentProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "A valid access token is required."
            });
        }

        var currentProfileId = profileId.Value;
        var messageText = request.MessageText?.Trim();

        var validationError = ValidateMessageText(messageText);

        if (validationError != null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        var contact = await _context.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(contact =>
                contact.ContactId == request.ContactId
            );

        if (contact == null)
        {
            return NotFound(new
            {
                message = "The contact was not found."
            });
        }

        if (contact.ProfileId == null)
        {
            return BadRequest(new
            {
                message = "This contact does not have a CCB profile."
            });
        }

        if (contact.ProfileId == currentProfileId)
        {
            return BadRequest(new
            {
                message = "You cannot send a message to yourself."
            });
        }

        var isMyContact = await _context.ProfileContacts
            .AnyAsync(profileContact =>
                profileContact.ProfileId == currentProfileId &&
                profileContact.ContactId == request.ContactId
            );

        if (!isMyContact)
        {
            return Forbid();
        }

        var message = new Message
        {
            ContactId = request.ContactId,
            SenderProfileId = currentProfileId,
            RecipientProfileId = contact.ProfileId.Value,
            MessageText = messageText!,
            IsSent = true,
            Translated = false,
            SentAt = DateTime.UtcNow,
            ReadAt = null,
            EditedAt = null,
            IsDeleted = false
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        var recipientMessage = new
        {
            messageId = message.MessageId,
            senderProfileId = message.SenderProfileId,
            recipientProfileId = message.RecipientProfileId,
            messageText = message.MessageText,
            isMine = false,
            translated = message.Translated,
            sentAt = message.SentAt,
            readAt = message.ReadAt,
            editedAt = message.EditedAt
        };

        var senderMessage = new
        {
            messageId = message.MessageId,
            senderProfileId = message.SenderProfileId,
            recipientProfileId = message.RecipientProfileId,
            messageText = message.MessageText,
            isMine = true,
            translated = message.Translated,
            sentAt = message.SentAt,
            readAt = message.ReadAt,
            editedAt = message.EditedAt
        };

        // Push the message to the recipient immediately.
        await _chatHub.Clients
            .Group($"profile-{message.RecipientProfileId}")
            .SendAsync(
                "MessageReceived",
                recipientMessage
            );

        // Keep the sender's other connected devices in sync.
        await _chatHub.Clients
            .Group($"profile-{message.SenderProfileId}")
            .SendAsync(
                "MessageReceived",
                senderMessage
            );

        return Ok(new
        {
            message = "Message sent successfully.",
            data = senderMessage
        });
    }

    // PUT: api/Messages/5
    [HttpPut("{messageId:long}")]
    public async Task<IActionResult> EditMessage(
        long messageId,
        EditMessageRequest request
    )
    {
        var profileId = GetCurrentProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "A valid access token is required."
            });
        }

        var messageText = request.MessageText?.Trim();
        var validationError = ValidateMessageText(messageText);

        if (validationError != null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        var message = await _context.Messages
            .FirstOrDefaultAsync(message =>
                message.MessageId == messageId &&
                !message.IsDeleted
            );

        if (message == null)
        {
            return NotFound(new
            {
                message = "The message was not found."
            });
        }

        if (message.SenderProfileId != profileId.Value)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "You can only edit messages that you sent."
                }
            );
        }

        message.MessageText = messageText!;
        message.EditedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Message updated successfully.",
            data = CreateMessageResponse(message, profileId.Value)
        });
    }

    // DELETE: api/Messages/5
    [HttpDelete("{messageId:long}")]
    public async Task<IActionResult> DeleteMessage(long messageId)
    {
        var profileId = GetCurrentProfileId();

        if (profileId == null)
        {
            return Unauthorized(new
            {
                message = "A valid access token is required."
            });
        }

        var message = await _context.Messages
            .FirstOrDefaultAsync(message =>
                message.MessageId == messageId &&
                !message.IsDeleted
            );

        if (message == null)
        {
            return NotFound(new
            {
                message = "The message was not found."
            });
        }

        if (message.SenderProfileId != profileId.Value)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "You can only delete messages that you sent."
                }
            );
        }

        message.IsDeleted = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Message deleted successfully.",
            messageId = message.MessageId
        });
    }

    private static string? ValidateMessageText(string? messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return "Enter a message.";
        }

        return messageText.Length > 4000
            ? "The message cannot be longer than 4000 characters."
            : null;
    }

    private static object CreateMessageResponse(
        Message message,
        int currentProfileId
    )
    {
        return new
        {
            messageId = message.MessageId,
            senderProfileId = message.SenderProfileId,
            recipientProfileId = message.RecipientProfileId,
            messageText = message.MessageText,
            isMine = message.SenderProfileId == currentProfileId,
            translated = message.Translated,
            sentAt = message.SentAt,
            readAt = message.ReadAt,
            editedAt = message.EditedAt
        };
    }

    private int? GetCurrentProfileId()
    {
        var profileIdValue = User.FindFirstValue("profileId");

        return int.TryParse(
            profileIdValue,
            out var profileId
        )
            ? profileId
            : null;
    }
}

public class SendMessageRequest
{
    public int ContactId { get; set; }

    public string MessageText { get; set; } =
        string.Empty;
}

public class EditMessageRequest
{
    public string MessageText { get; set; } =
        string.Empty;
}
