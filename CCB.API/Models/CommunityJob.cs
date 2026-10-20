using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class CommunityJob
{
    public int CommunityJobId { get; set; }

    public int CommunityId { get; set; }

    public string Title { get; set; } = null!;

    public string Detail { get; set; } = null!;

    public DateTime PostedAt { get; set; }

    public virtual Community Community { get; set; } = null!;
}
