using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using CCB.API.Data;
using CCB.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly CCBDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AuthController(
        CCBDbContext context,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode(
        SendCodeRequest request)
    {
        var phone = NormalizeSouthAfricanPhone(
            request.Phone
        );

        if (phone == null)
        {
            return BadRequest(new
            {
                message =
                    "Enter a valid South African mobile number."
            });
        }

        var activeCodes =
            await _context.PhoneVerificationCodes
                .Where(code =>
                    code.Phone == phone &&
                    !code.IsUsed)
                .ToListAsync();

        foreach (var activeCode in activeCodes)
        {
            activeCode.IsUsed = true;
        }

        const string verificationCode = "123456";

        var phoneVerification =
            new PhoneVerificationCode
            {
                Phone = phone,

                CodeHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        verificationCode
                    ),

                ExpiresAt =
                    DateTime.UtcNow.AddMinutes(5),

                IsUsed = false,

                AttemptCount = 0,

                CreatedAt = DateTime.UtcNow
            };

        _context.PhoneVerificationCodes.Add(
            phoneVerification
        );

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Verification code created successfully.",

            phone,

            expiresInSeconds = 300,

            developmentCode = _environment.IsDevelopment()
                ? verificationCode
                : null
        });
    }

    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode(
        VerifyCodeRequest request)
    {
        var phone = NormalizeSouthAfricanPhone(
            request.Phone
        );

        if (phone == null)
        {
            return BadRequest(new
            {
                message =
                    "Enter a valid South African mobile number."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Code) ||
            request.Code.Length != 6)
        {
            return BadRequest(new
            {
                message =
                    "Enter the six-digit verification code."
            });
        }

        var verification =
            await _context.PhoneVerificationCodes
                .Where(code =>
                    code.Phone == phone &&
                    !code.IsUsed)
                .OrderByDescending(code => code.CreatedAt)
                .FirstOrDefaultAsync();

        if (verification == null)
        {
            return BadRequest(new
            {
                message =
                    "No active verification code was found. Request a new code."
            });
        }

        if (verification.ExpiresAt <= DateTime.UtcNow)
        {
            verification.IsUsed = true;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "The verification code has expired. Request a new code."
            });
        }

        if (verification.AttemptCount >= 5)
        {
            verification.IsUsed = true;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "Too many incorrect attempts. Request a new code."
            });
        }

        var codeIsCorrect =
            BCrypt.Net.BCrypt.Verify(
                request.Code,
                verification.CodeHash
            );

        if (!codeIsCorrect)
        {
            verification.AttemptCount++;

            if (verification.AttemptCount >= 5)
            {
                verification.IsUsed = true;
            }

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "The verification code is incorrect.",

                attemptsRemaining =
                    Math.Max(
                        0,
                        5 - verification.AttemptCount
                    )
            });
        }

        verification.IsUsed = true;

        var profile = await _context.Profiles
            .FirstOrDefaultAsync(p => p.Phone == phone);

        await _context.SaveChangesAsync();

        if (profile == null)
        {
            var setupToken = CreateToken(
                phone: phone,
                profile: null,
                tokenType: "profile_setup",
                expiresInMinutes: 15
            );

            return Ok(new
            {
                message =
                    "Phone number verified successfully.",

                isNewUser = true,

                nextScreen = "ProfileSetup",

                phone,

                setupToken
            });
        }

        var accessToken = CreateToken(
            phone: phone,
            profile: profile,
            tokenType: "access",
            expiresInMinutes: GetAccessTokenMinutes()
        );

        return Ok(new
        {
            message = "Login successful.",

            isNewUser = false,

            nextScreen = "Home",

            token = accessToken,

            user = new
            {
                profileId = profile.ProfileId,
                name = profile.Name,
                username = profile.Username,
                phone = profile.Phone,
                language = profile.Language,
                avatarColor = profile.AvatarColor,
                balance = profile.Balance,
                darkMode = profile.DarkMode
            }
        });
    }

    private string CreateToken(
        string phone,
        Profile? profile,
        string tokenType,
        int expiresInMinutes)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key is missing."
            );

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                profile?.ProfileId.ToString() ?? phone
            ),

            new("phone", phone),

            new("tokenType", tokenType)
        };

        if (profile != null)
        {
            claims.Add(
                new Claim(
                    "profileId",
                    profile.ProfileId.ToString()
                )
            );

            claims.Add(
                new Claim(
                    "username",
                    profile.Username ?? string.Empty
                )
            );
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        );

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                expiresInMinutes
            ),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private int GetAccessTokenMinutes()
    {
        return int.TryParse(
            _configuration["Jwt:ExpirationMinutes"],
            out var minutes
        )
            ? minutes
            : 1440;
    }

    private static string?
        NormalizeSouthAfricanPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var phone = Regex.Replace(
            value,
            @"[\s\-\(\)]",
            string.Empty
        );

        if (phone.StartsWith("0"))
        {
            phone = "+27" + phone[1..];
        }
        else if (phone.StartsWith("27"))
        {
            phone = "+" + phone;
        }

        return Regex.IsMatch(
            phone,
            @"^\+27[6-8][0-9]{8}$"
        )
            ? phone
            : null;
    }
}

public class SendCodeRequest
{
    public string Phone { get; set; } =
        string.Empty;
}

public class VerifyCodeRequest
{
    public string Phone { get; set; } =
        string.Empty;

    public string Code { get; set; } =
        string.Empty;
}