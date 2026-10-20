using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Status
{
    public long StatusId { get; set; }

    public int ContactId { get; set; }

    public string Caption { get; set; } = null!;

    public string? BackgroundColor { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool Viewed { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual Contact Contact { get; set; } = null!;
}
