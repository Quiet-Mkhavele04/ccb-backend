using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CCB.API.Data;
using CCB.API.Models;
using CCB.API.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private const int MaximumPinAttempts = 5;
    private const int MaximumOtpAttempts = 5;
    private static readonly TimeSpan PinLockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PaymentRequestLifetime = TimeSpan.FromDays(7);

    private readonly CCBDbContext _context;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public TransactionsController(
        CCBDbContext context,
        IPaymentProvider paymentProvider,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _context = context;
        _paymentProvider = paymentProvider;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet()
    {
        var profileId = GetProfileId();
        if (profileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var wallet = await _context.Profiles
            .AsNoTracking()
            .Where(profile => profile.ProfileId == profileId.Value)
            .Select(profile => new
            {
                balance = profile.Balance,
                currency = "ZAR"
            })
            .FirstOrDefaultAsync();

        return wallet == null
            ? NotFound(new { message = "Profile not found." })
            : Ok(new { wallet });
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions()
    {
        var profileId = GetProfileId();
        if (profileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var currentProfileId = profileId.Value;

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.SenderProfileId == currentProfileId ||
                transaction.RecipientProfileId == currentProfileId)
            .OrderByDescending(transaction => transaction.TransactionDate)
            .Select(transaction => new
            {
                transactionId = transaction.TransactionId,
                counterparty = transaction.SenderProfileId == currentProfileId
                    ? transaction.RecipientProfile!.Name
                    : transaction.SenderProfile!.Name,
                amount = transaction.Amount,
                direction = transaction.SenderProfileId == currentProfileId
                    ? "sent"
                    : "received",
                transactionDate = transaction.TransactionDate,
                reference = transaction.Reference,
                description = transaction.Description,
                status = transaction.Status
            })
            .ToListAsync();

        return Ok(new { transactions });
    }

    [HttpGet("pin/status")]
    public async Task<IActionResult> GetPinStatus()
    {
        var profileId = GetProfileId();
        if (profileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var hasPin = await _context.PaymentPins
            .AsNoTracking()
            .AnyAsync(pin => pin.ProfileId == profileId.Value);

        return Ok(new { hasPin });
    }

    [HttpPost("pin")]
    public async Task<IActionResult> SetPin([FromBody] SetPaymentPinRequest request)
    {
        var profileId = GetProfileId();
        if (profileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        if (!IsValidPin(request.Pin) || request.Pin != request.ConfirmPin)
        {
            return BadRequest(new
            {
                message = "Enter matching four-digit payment PINs."
            });
        }

        var profileExists = await _context.Profiles
            .AnyAsync(profile => profile.ProfileId == profileId.Value);

        if (!profileExists)
        {
            return NotFound(new { message = "Profile not found." });
        }

        var paymentPin = await _context.PaymentPins
            .FirstOrDefaultAsync(pin => pin.ProfileId == profileId.Value);

        var now = DateTime.UtcNow;
        var pinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin, 12);

        if (paymentPin == null)
        {
            paymentPin = new PaymentPin
            {
                ProfileId = profileId.Value,
                PinHash = pinHash,
                FailedAttempts = 0,
                CreatedAt = now
            };

            _context.PaymentPins.Add(paymentPin);
        }
        else
        {
            paymentPin.PinHash = pinHash;
            paymentPin.FailedAttempts = 0;
            paymentPin.LockedUntil = null;
            paymentPin.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Payment PIN saved successfully." });
    }

    [HttpPost("transfer/start")]
    public async Task<IActionResult> StartTransfer(
        [FromBody] StartTransferRequest request)
    {
        var senderProfileId = GetProfileId();
        if (senderProfileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Enter an amount greater than zero." });
        }

        if (request.Amount > 100000m)
        {
            return BadRequest(new { message = "The maximum demo transfer is R100,000.00." });
        }

        if (request.RecipientProfileId == senderProfileId.Value)
        {
            return BadRequest(new { message = "You cannot send money to yourself." });
        }

        if (!IsValidPin(request.Pin))
        {
            return BadRequest(new { message = "Enter your four-digit payment PIN." });
        }

        var paymentPin = await _context.PaymentPins
            .FirstOrDefaultAsync(pin => pin.ProfileId == senderProfileId.Value);

        if (paymentPin == null)
        {
            return Conflict(new
            {
                message = "Create your payment PIN before sending money.",
                requiresPinSetup = true
            });
        }

        var now = DateTime.UtcNow;

        if (paymentPin.LockedUntil.HasValue && paymentPin.LockedUntil.Value > now)
        {
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = "Your payment PIN is temporarily locked.",
                lockedUntil = paymentPin.LockedUntil
            });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Pin, paymentPin.PinHash))
        {
            paymentPin.FailedAttempts += 1;

            if (paymentPin.FailedAttempts >= MaximumPinAttempts)
            {
                paymentPin.LockedUntil = now.Add(PinLockDuration);
            }

            paymentPin.UpdatedAt = now;
            await _context.SaveChangesAsync();

            return Unauthorized(new
            {
                message = paymentPin.LockedUntil.HasValue
                    ? "Too many incorrect attempts. Your payment PIN is locked for 15 minutes."
                    : "The payment PIN is incorrect."
            });
        }

        paymentPin.FailedAttempts = 0;
        paymentPin.LockedUntil = null;
        paymentPin.UpdatedAt = now;

        var sender = await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.ProfileId == senderProfileId.Value);

        var recipient = await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.ProfileId == request.RecipientProfileId);

        if (sender == null)
        {
            return NotFound(new { message = "Sender profile not found." });
        }

        if (recipient == null)
        {
            return NotFound(new { message = "Recipient profile not found." });
        }

        if (sender.Balance < request.Amount)
        {
            return BadRequest(new { message = "Your wallet balance is too low." });
        }

        await _context.PaymentVerificationCodes
            .Where(code =>
                code.SenderProfileId == senderProfileId.Value &&
                !code.IsUsed)
            .ExecuteUpdateAsync(update => update.SetProperty(code => code.IsUsed, true));

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var challenge = new PaymentVerificationCode
        {
            ChallengeId = Guid.NewGuid(),
            SenderProfileId = senderProfileId.Value,
            RecipientProfileId = recipient.ProfileId,
            Amount = decimal.Round(request.Amount, 2),
            Reference = CleanReference(request.Reference),
            CodeHash = HashOtp(otp),
            ExpiresAt = now.Add(OtpLifetime),
            FailedAttempts = 0,
            IsUsed = false,
            CreatedAt = now
        };

        _context.PaymentVerificationCodes.Add(challenge);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Verification code created successfully.",
            challengeId = challenge.ChallengeId,
            expiresInSeconds = (int)OtpLifetime.TotalSeconds,
            recipient = new
            {
                profileId = recipient.ProfileId,
                name = recipient.Name
            },
            amount = challenge.Amount,
            developmentCode = _environment.IsDevelopment() ? otp : null
        });
    }

    [HttpPost("transfer/confirm")]
    public async Task<IActionResult> ConfirmTransfer(
        [FromBody] ConfirmTransferRequest request,
        CancellationToken cancellationToken)
    {
        var senderProfileId = GetProfileId();
        if (senderProfileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        if (!Regex.IsMatch(request.Code ?? string.Empty, @"^\d{6}$"))
        {
            return BadRequest(new { message = "Enter the six-digit verification code." });
        }

        await using var databaseTransaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var challenge = await _context.PaymentVerificationCodes
            .FirstOrDefaultAsync(code =>
                code.ChallengeId == request.ChallengeId &&
                code.SenderProfileId == senderProfileId.Value,
                cancellationToken);

        if (challenge == null)
        {
            return NotFound(new { message = "Payment verification request not found." });
        }

        if (challenge.IsUsed)
        {
            return Conflict(new { message = "This verification code has already been used." });
        }

        var now = DateTime.UtcNow;

        if (challenge.ExpiresAt <= now)
        {
            challenge.IsUsed = true;
            await _context.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);

            return BadRequest(new { message = "The verification code has expired." });
        }

        if (!OtpMatches(request.Code!, challenge.CodeHash))
        {
            challenge.FailedAttempts += 1;

            if (challenge.FailedAttempts >= MaximumOtpAttempts)
            {
                challenge.IsUsed = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);

            return Unauthorized(new
            {
                message = challenge.IsUsed
                    ? "Too many incorrect verification attempts. Start the transfer again."
                    : "The verification code is incorrect."
            });
        }

        PaymentRequest? paymentRequest = null;

        if (challenge.PaymentRequestId.HasValue)
        {
            paymentRequest = await _context.PaymentRequests
                .FirstOrDefaultAsync(item =>
                    item.PaymentRequestId == challenge.PaymentRequestId.Value,
                    cancellationToken);

            if (paymentRequest == null)
            {
                return NotFound(new { message = "The payment request no longer exists." });
            }

            if (paymentRequest.Status != "Pending")
            {
                return Conflict(new
                {
                    message = $"This payment request is already {paymentRequest.Status.ToLowerInvariant()}."
                });
            }

            if (paymentRequest.ExpiresAt <= now)
            {
                paymentRequest.Status = "Expired";
                paymentRequest.RespondedAt = now;
                challenge.IsUsed = true;
                await _context.SaveChangesAsync(cancellationToken);
                await databaseTransaction.CommitAsync(cancellationToken);

                return BadRequest(new { message = "This payment request has expired." });
            }

            if (paymentRequest.PayerProfileId != senderProfileId.Value ||
                paymentRequest.RequesterProfileId != challenge.RecipientProfileId ||
                paymentRequest.Amount != challenge.Amount)
            {
                return BadRequest(new { message = "The payment request details are invalid." });
            }
        }

        var sender = await _context.Profiles
            .FirstOrDefaultAsync(profile => profile.ProfileId == senderProfileId.Value, cancellationToken);

        var recipient = await _context.Profiles
            .FirstOrDefaultAsync(profile => profile.ProfileId == challenge.RecipientProfileId, cancellationToken);

        if (sender == null || recipient == null)
        {
            return NotFound(new { message = "The sender or recipient profile no longer exists." });
        }

        if (sender.Balance < challenge.Amount)
        {
            return BadRequest(new { message = "Your wallet balance is too low." });
        }

        var providerResult = await _paymentProvider.ProcessTransferAsync(
            new PaymentProviderTransferRequest(
                sender.ProfileId,
                recipient.ProfileId,
                challenge.Amount,
                challenge.Reference),
            cancellationToken);

        if (!providerResult.Success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = providerResult.ErrorMessage ?? "The payment provider rejected the transfer."
            });
        }

        sender.Balance -= challenge.Amount;
        recipient.Balance += challenge.Amount;

        var transaction = new Transaction
        {
            SenderProfileId = sender.ProfileId,
            RecipientProfileId = recipient.ProfileId,
            Counterparty = recipient.Name ?? recipient.Username ?? "CCB user",
            Amount = challenge.Amount,
            Direction = "sent",
            TransactionDate = now,
            Reference = challenge.Reference,
            Description = providerResult.ProviderReference,
            Status = providerResult.Status
        };

        challenge.IsUsed = true;
        _context.Transactions.Add(transaction);

        if (paymentRequest != null)
        {
            paymentRequest.Status = "Paid";
            paymentRequest.RespondedAt = now;
            paymentRequest.Transaction = transaction;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            message = "Money sent successfully.",
            transaction = new
            {
                transactionId = transaction.TransactionId,
                recipientProfileId = recipient.ProfileId,
                recipientName = recipient.Name,
                amount = transaction.Amount,
                reference = transaction.Reference,
                status = transaction.Status,
                transactionDate = transaction.TransactionDate
            },
            walletBalance = sender.Balance
        });
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreatePaymentRequest(
        [FromBody] CreatePaymentRequestRequest request)
    {
        var requesterProfileId = GetProfileId();
        if (requesterProfileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        if (request.Amount <= 0 || request.Amount > 100000m)
        {
            return BadRequest(new
            {
                message = "Enter a request amount between R0.01 and R100,000.00."
            });
        }

        if (request.PayerProfileId == requesterProfileId.Value)
        {
            return BadRequest(new { message = "You cannot request money from yourself." });
        }

        var payer = await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.ProfileId == request.PayerProfileId);

        if (payer == null)
        {
            return NotFound(new { message = "The selected payer was not found." });
        }

        var now = DateTime.UtcNow;
        var paymentRequest = new PaymentRequest
        {
            RequesterProfileId = requesterProfileId.Value,
            PayerProfileId = payer.ProfileId,
            Amount = decimal.Round(request.Amount, 2),
            Reference = CleanReference(request.Reference),
            Status = "Pending",
            CreatedAt = now,
            ExpiresAt = now.Add(PaymentRequestLifetime)
        };

        _context.PaymentRequests.Add(paymentRequest);
        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Payment request sent successfully.",
            paymentRequest = new
            {
                paymentRequestId = paymentRequest.PaymentRequestId,
                payerProfileId = payer.ProfileId,
                payerName = payer.Name,
                amount = paymentRequest.Amount,
                reference = paymentRequest.Reference,
                status = paymentRequest.Status,
                createdAt = paymentRequest.CreatedAt,
                expiresAt = paymentRequest.ExpiresAt
            }
        });
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetPaymentRequests()
    {
        var profileId = GetProfileId();
        if (profileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var currentProfileId = profileId.Value;
        var now = DateTime.UtcNow;

        await _context.PaymentRequests
            .Where(item =>
                item.Status == "Pending" &&
                item.ExpiresAt <= now &&
                (item.RequesterProfileId == currentProfileId ||
                 item.PayerProfileId == currentProfileId))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, "Expired")
                .SetProperty(item => item.RespondedAt, now));

        var requests = await _context.PaymentRequests
            .AsNoTracking()
            .Where(item =>
                item.RequesterProfileId == currentProfileId ||
                item.PayerProfileId == currentProfileId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                paymentRequestId = item.PaymentRequestId,
                direction = item.RequesterProfileId == currentProfileId
                    ? "outgoing"
                    : "incoming",
                requesterProfileId = item.RequesterProfileId,
                requesterName = item.RequesterProfile.Name,
                payerProfileId = item.PayerProfileId,
                payerName = item.PayerProfile.Name,
                amount = item.Amount,
                reference = item.Reference,
                status = item.Status,
                createdAt = item.CreatedAt,
                expiresAt = item.ExpiresAt,
                respondedAt = item.RespondedAt,
                transactionId = item.TransactionId
            })
            .ToListAsync();

        return Ok(new { requests });
    }

    [HttpPost("requests/{paymentRequestId:long}/cancel")]
    public async Task<IActionResult> CancelPaymentRequest(long paymentRequestId)
    {
        var profileId = GetProfileId();
        if (profileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var paymentRequest = await _context.PaymentRequests
            .FirstOrDefaultAsync(item =>
                item.PaymentRequestId == paymentRequestId &&
                item.RequesterProfileId == profileId.Value);

        if (paymentRequest == null)
        {
            return NotFound(new { message = "Payment request not found." });
        }

        if (paymentRequest.Status != "Pending")
        {
            return Conflict(new { message = "Only a pending request can be cancelled." });
        }

        paymentRequest.Status = "Cancelled";
        paymentRequest.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Payment request cancelled successfully." });
    }

    [HttpPost("requests/{paymentRequestId:long}/decline")]
    public async Task<IActionResult> DeclinePaymentRequest(long paymentRequestId)
    {
        var profileId = GetProfileId();
        if (profileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        var paymentRequest = await _context.PaymentRequests
            .FirstOrDefaultAsync(item =>
                item.PaymentRequestId == paymentRequestId &&
                item.PayerProfileId == profileId.Value);

        if (paymentRequest == null)
        {
            return NotFound(new { message = "Payment request not found." });
        }

        if (paymentRequest.Status != "Pending")
        {
            return Conflict(new { message = "Only a pending request can be declined." });
        }

        paymentRequest.Status = "Declined";
        paymentRequest.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Payment request declined successfully." });
    }

    [HttpPost("requests/{paymentRequestId:long}/pay/start")]
    public async Task<IActionResult> StartPaymentRequestPayment(
        long paymentRequestId,
        [FromBody] PayPaymentRequestRequest request)
    {
        var payerProfileId = GetProfileId();
        if (payerProfileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        if (!IsValidPin(request.Pin))
        {
            return BadRequest(new { message = "Enter your four-digit payment PIN." });
        }

        var paymentRequest = await _context.PaymentRequests
            .Include(item => item.RequesterProfile)
            .FirstOrDefaultAsync(item =>
                item.PaymentRequestId == paymentRequestId &&
                item.PayerProfileId == payerProfileId.Value);

        if (paymentRequest == null)
        {
            return NotFound(new { message = "Payment request not found." });
        }

        var now = DateTime.UtcNow;

        if (paymentRequest.Status != "Pending")
        {
            return Conflict(new { message = "This payment request is no longer pending." });
        }

        if (paymentRequest.ExpiresAt <= now)
        {
            paymentRequest.Status = "Expired";
            paymentRequest.RespondedAt = now;
            await _context.SaveChangesAsync();
            return BadRequest(new { message = "This payment request has expired." });
        }

        var paymentPin = await _context.PaymentPins
            .FirstOrDefaultAsync(pin => pin.ProfileId == payerProfileId.Value);

        if (paymentPin == null)
        {
            return Conflict(new
            {
                message = "Create your payment PIN before paying a request.",
                requiresPinSetup = true
            });
        }

        if (paymentPin.LockedUntil.HasValue && paymentPin.LockedUntil.Value > now)
        {
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = "Your payment PIN is temporarily locked.",
                lockedUntil = paymentPin.LockedUntil
            });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Pin, paymentPin.PinHash))
        {
            paymentPin.FailedAttempts += 1;
            if (paymentPin.FailedAttempts >= MaximumPinAttempts)
            {
                paymentPin.LockedUntil = now.Add(PinLockDuration);
            }

            paymentPin.UpdatedAt = now;
            await _context.SaveChangesAsync();

            return Unauthorized(new
            {
                message = paymentPin.LockedUntil.HasValue
                    ? "Too many incorrect attempts. Your payment PIN is locked for 15 minutes."
                    : "The payment PIN is incorrect."
            });
        }

        paymentPin.FailedAttempts = 0;
        paymentPin.LockedUntil = null;
        paymentPin.UpdatedAt = now;

        var payer = await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.ProfileId == payerProfileId.Value);

        if (payer == null)
        {
            return NotFound(new { message = "Payer profile not found." });
        }

        if (payer.Balance < paymentRequest.Amount)
        {
            return BadRequest(new { message = "Your wallet balance is too low." });
        }

        await _context.PaymentVerificationCodes
            .Where(code => code.SenderProfileId == payerProfileId.Value && !code.IsUsed)
            .ExecuteUpdateAsync(update => update.SetProperty(code => code.IsUsed, true));

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var challenge = new PaymentVerificationCode
        {
            ChallengeId = Guid.NewGuid(),
            SenderProfileId = payerProfileId.Value,
            RecipientProfileId = paymentRequest.RequesterProfileId,
            PaymentRequestId = paymentRequest.PaymentRequestId,
            Amount = paymentRequest.Amount,
            Reference = paymentRequest.Reference,
            CodeHash = HashOtp(otp),
            ExpiresAt = now.Add(OtpLifetime),
            FailedAttempts = 0,
            IsUsed = false,
            CreatedAt = now
        };

        _context.PaymentVerificationCodes.Add(challenge);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Verification code created successfully.",
            challengeId = challenge.ChallengeId,
            expiresInSeconds = (int)OtpLifetime.TotalSeconds,
            paymentRequestId = paymentRequest.PaymentRequestId,
            recipient = new
            {
                profileId = paymentRequest.RequesterProfileId,
                name = paymentRequest.RequesterProfile.Name
            },
            amount = challenge.Amount,
            developmentCode = _environment.IsDevelopment() ? otp : null
        });
    }

    [HttpPost("development/top-up")]
    public async Task<IActionResult> DevelopmentTopUp(
        [FromBody] DevelopmentTopUpRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var profileId = GetProfileId();
        if (profileId == null)
        {
            return Unauthorized(new { message = "Your login session is invalid." });
        }

        if (request.Amount <= 0 || request.Amount > 10000m)
        {
            return BadRequest(new { message = "Enter a demo top-up between R0.01 and R10,000.00." });
        }

        var profile = await _context.Profiles
            .FirstOrDefaultAsync(item => item.ProfileId == profileId.Value);

        if (profile == null)
        {
            return NotFound(new { message = "Profile not found." });
        }

        profile.Balance += decimal.Round(request.Amount, 2);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Demo wallet funds added successfully.",
            walletBalance = profile.Balance,
            currency = "ZAR"
        });
    }

    private int? GetProfileId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("profileId")
            ?? User.FindFirstValue("ProfileId");

        return int.TryParse(value, out var profileId) ? profileId : null;
    }

    private static bool IsValidPin(string? pin) =>
        Regex.IsMatch(pin ?? string.Empty, @"^\d{4}$");

    private static string? CleanReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var cleaned = reference.Trim();
        return cleaned.Length <= 100 ? cleaned : cleaned[..100];
    }

    private string HashOtp(string code)
    {
        var secret = _configuration["Payment:OtpPepper"]
            ?? _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Payment OTP security key is missing.");

        return Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(code)));
    }

    private bool OtpMatches(string code, string expectedHash)
    {
        var actualHash = HashOtp(code);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actualHash),
            Encoding.ASCII.GetBytes(expectedHash));
    }
}

public sealed class SetPaymentPinRequest
{
    public string Pin { get; set; } = string.Empty;

    public string ConfirmPin { get; set; } = string.Empty;
}

public sealed class StartTransferRequest
{
    public int RecipientProfileId { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string Pin { get; set; } = string.Empty;
}

public sealed class ConfirmTransferRequest
{
    public Guid ChallengeId { get; set; }

    public string Code { get; set; } = string.Empty;
}

public sealed class DevelopmentTopUpRequest
{
    public decimal Amount { get; set; }
}

public sealed class CreatePaymentRequestRequest
{
    public int PayerProfileId { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }
}

public sealed class PayPaymentRequestRequest
{
    public string Pin { get; set; } = string.Empty;
}
