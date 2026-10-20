using System;

namespace CCB.API.Models;

public partial class Transaction
{
    public long TransactionId { get; set; }

    public string Counterparty { get; set; } =
        null!;

    public decimal Amount { get; set; }

    public string Direction { get; set; } =
        null!;

    public DateTime TransactionDate { get; set; }

    public string? Reference { get; set; }

    public string? Description { get; set; }

    public int? SenderProfileId { get; set; }

    public int? RecipientProfileId { get; set; }

    public string Status { get; set; } =
        null!;

    public virtual Profile? SenderProfile {
        get;
        set;
    }

    public virtual Profile? RecipientProfile {
        get;
        set;
    }
}