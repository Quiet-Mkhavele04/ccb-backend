using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Contact
{
    public int ContactId { get; set; }

    // Links this contact to a registered CCB profile.
    // Nullable because some contacts may not have a CCB account.
    public int? ProfileId { get; set; }

    public string Name { get; set; } = null!;

    public string? AvatarColor { get; set; }

    public bool IsOnline { get; set; }

    public string? Language { get; set; }

    public bool Pinned { get; set; }

    public int UnreadCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Profile? Profile { get; set; }

    public virtual ICollection<BlockedContact> BlockedContacts { get; set; }
        = new List<BlockedContact>();

    public virtual ICollection<Message> Messages { get; set; }
        = new List<Message>();

    public virtual ICollection<ProfileContact> ProfileContacts { get; set; }
        = new List<ProfileContact>();

    public virtual ICollection<Report> Reports { get; set; }
        = new List<Report>();

    public virtual ICollection<Status> Statuses { get; set; }
        = new List<Status>();
}