using System;

namespace CCB.API.Models;

public class PaymentVerificationCode
{
    public long PaymentVerificationCodeId
    {
        get;
        set;
    }

    public Guid ChallengeId
    {
        get;
        set;
    }

    public int SenderProfileId
    {
        get;
        set;
    }

    public int RecipientProfileId
    {
        get;
        set;
    }

    public long? PaymentRequestId
    {
        get;
        set;
    }

    public decimal Amount
    {
        get;
        set;
    }

    public string? Reference
    {
        get;
        set;
    }

    public string CodeHash
    {
        get;
        set;
    } = null!;

    public DateTime ExpiresAt
    {
        get;
        set;
    }

    public int FailedAttempts
    {
        get;
        set;
    }

    public bool IsUsed
    {
        get;
        set;
    }

    public DateTime CreatedAt
    {
        get;
        set;
    }

    public virtual Profile SenderProfile
    {
        get;
        set;
    } = null!;

    public virtual Profile RecipientProfile
    {
        get;
        set;
    } = null!;

    public virtual PaymentRequest? PaymentRequest
    {
        get;
        set;
    }
}