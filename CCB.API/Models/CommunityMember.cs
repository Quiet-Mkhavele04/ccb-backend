using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class CommunityMember
{
    public int CommunityMemberId { get; set; }

    public int CommunityId { get; set; }

    public int ProfileId { get; set; }

    public DateTime JoinedAt { get; set; }

    public bool IsAdmin { get; set; }

    public virtual Community Community { get; set; } = null!;

    public virtual Profile Profile { get; set; } = null!;
}
