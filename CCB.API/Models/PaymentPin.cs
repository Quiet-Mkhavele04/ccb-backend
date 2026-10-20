using System;

namespace CCB.API.Models;

public class PaymentPin
{
    public int PaymentPinId { get; set; }

    public int ProfileId { get; set; }

    public string PinHash { get; set; } =
        null!;

    public int FailedAttempts { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Profile Profile { get; set; } =
        null!;
}