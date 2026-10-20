using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class CommunityListing
{
    public int CommunityListingId { get; set; }

    public int CommunityId { get; set; }

    public string Title { get; set; } = null!;

    public decimal? Price { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Community Community { get; set; } = null!;
}
