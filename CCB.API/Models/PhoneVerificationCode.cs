namespace CCB.API.Models;

public class PhoneVerificationCode
{
    public int PhoneVerificationCodeId { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; }
}