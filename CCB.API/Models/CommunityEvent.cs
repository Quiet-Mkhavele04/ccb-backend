using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class CommunityEvent
{
    public int CommunityEventId { get; set; }

    public int CommunityId { get; set; }

    public string Title { get; set; } = null!;

    public string? DateLabel { get; set; }

    public DateTime? EventDate { get; set; }

    public string? Location { get; set; }

    public string? Description { get; set; }

    public virtual Community Community { get; set; } = null!;
}
