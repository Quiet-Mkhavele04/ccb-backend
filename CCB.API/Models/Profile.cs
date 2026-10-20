using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Profile
{
    public int ProfileId { get; set; }

    public string? Name { get; set; }

    public string? Username { get; set; }

    public string? Phone { get; set; }

    public string? AvatarColor { get; set; }

    

    public string? Language { get; set; }

    public decimal Balance { get; set; }

    public bool DarkMode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<BlockedContact> BlockedContacts { get; set; } = new List<BlockedContact>();

    public virtual ICollection<BusinessOwner> BusinessOwners { get; set; } = new List<BusinessOwner>();

    public virtual ICollection<CommunityMember> CommunityMembers { get; set; } = new List<CommunityMember>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<ProfileContact> ProfileContacts { get; set; } = new List<ProfileContact>();

    public virtual ICollection<Report> Reports { get; set; } = new List<Report>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual UserAccount? UserAccount { get; set; }

    public virtual UserSetting? UserSetting { get; set; }
}
