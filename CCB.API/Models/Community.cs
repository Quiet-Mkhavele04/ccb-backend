using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Community
{
    public int CommunityId { get; set; }

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public string? BannerColor { get; set; }

    public int MemberCount { get; set; }

    public bool Joined { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CommunityEvent> CommunityEvents { get; set; } = new List<CommunityEvent>();

    public virtual ICollection<CommunityJob> CommunityJobs { get; set; } = new List<CommunityJob>();

    public virtual ICollection<CommunityListing> CommunityListings { get; set; } = new List<CommunityListing>();

    public virtual ICollection<CommunityMember> CommunityMembers { get; set; } = new List<CommunityMember>();

    public virtual ICollection<CommunityMessage> CommunityMessages { get; set; } = new List<CommunityMessage>();
}
