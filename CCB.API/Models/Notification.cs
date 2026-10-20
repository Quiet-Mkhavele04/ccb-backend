using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Notification
{
    public long NotificationId { get; set; }

    public int ProfileId { get; set; }

    public string Title { get; set; } = null!;

    public string? Message { get; set; }

    public string? NotificationType { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Profile Profile { get; set; } = null!;
}
