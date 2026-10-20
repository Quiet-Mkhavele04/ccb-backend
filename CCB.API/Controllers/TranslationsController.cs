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
public class TranslationsController : ControllerBase
{
    private readonly CCBDbContext _context;

    public TranslationsController(
        CCBDbContext context
    )
    {
        _context = context;
    }

    // POST:
    // api/Translations/message/1
    [HttpPost("message/{messageId:long}")]
    public async Task<IActionResult> TranslateMessage(
        long messageId
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

        var message = await _context.Messages
            .FirstOrDefaultAsync(item =>
                item.MessageId == messageId &&
                !item.IsDeleted
            );

        if (message == null)
        {
            return NotFound(new
            {
                message =
                    "The message was not found."
            });
        }

        var canAccessMessage =
            message.SenderProfileId ==
                profileId.Value ||
            message.RecipientProfileId ==
                profileId.Value;

        if (!canAccessMessage)
        {
            return Forbid();
        }

        const string sourceLanguage = "English";
        const string targetLanguage = "English";

        var existingTranslation =
            await _context.Translations
                .AsNoTracking()
                .FirstOrDefaultAsync(translation =>
                    translation.MessageId ==
                        messageId &&
                    translation.TargetLanguage ==
                        targetLanguage
                );

        if (existingTranslation != null)
        {
            return Ok(new
            {
                message =
                    "Translation loaded successfully.",

                translation = new
                {
                    translationId =
                        existingTranslation
                            .TranslationId,

                    messageId =
                        existingTranslation
                            .MessageId,

                    sourceLanguage =
                        existingTranslation
                            .SourceLanguage,

                    targetLanguage =
                        existingTranslation
                            .TargetLanguage,

                    originalText =
                        existingTranslation
                            .OriginalText,

                    translatedText =
                        existingTranslation
                            .TranslatedText,

                    createdAt =
                        existingTranslation
                            .CreatedAt
                }
            });
        }

        // English-only translation for now.
        // The translated text stays the same.
        var translation = new Translation
        {
            MessageId = message.MessageId,

            SourceLanguage =
                sourceLanguage,

            TargetLanguage =
                targetLanguage,

            OriginalText =
                message.MessageText,

            TranslatedText =
                message.MessageText,

            CreatedAt =
                DateTime.UtcNow
        };

        _context.Translations.Add(
            translation
        );

        message.Translated = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Message translated successfully.",

            translation = new
            {
                translationId =
                    translation.TranslationId,

                messageId =
                    translation.MessageId,

                sourceLanguage =
                    translation.SourceLanguage,

                targetLanguage =
                    translation.TargetLanguage,

                originalText =
                    translation.OriginalText,

                translatedText =
                    translation.TranslatedText,

                createdAt =
                    translation.CreatedAt
            }
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
}