using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class CommunityMessage
{
    public long CommunityMessageId { get; set; }

    public int CommunityId { get; set; }

    public string SenderName { get; set; } = null!;

    public string? AvatarColor { get; set; }

    public string MessageText { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public bool IsMe { get; set; }

    public virtual Community Community { get; set; } = null!;
}
