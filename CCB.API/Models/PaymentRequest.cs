using System;

namespace CCB.API.Models;

public class PaymentRequest
{
    public long PaymentRequestId {
        get;
        set;
    }

    public int RequesterProfileId {
        get;
        set;
    }

    public int PayerProfileId {
        get;
        set;
    }

    public decimal Amount { get; set; }

    public string? Reference {
        get;
        set;
    }

    public string Status { get; set; } =
        null!;

    public DateTime CreatedAt {
        get;
        set;
    }

    public DateTime ExpiresAt {
        get;
        set;
    }

    public DateTime? RespondedAt {
        get;
        set;
    }

    public long? TransactionId {
        get;
        set;
    }

    public virtual Profile RequesterProfile {
        get;
        set;
    } = null!;

    public virtual Profile PayerProfile {
        get;
        set;
    } = null!;

    public virtual Transaction? Transaction {
        get;
        set;
    }
}