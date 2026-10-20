using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Event
{
    public int EventId { get; set; }

    public string Title { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? DateLabel { get; set; }

    public DateTime? EventDate { get; set; }

    public string? Location { get; set; }

    public string? BannerColor { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
