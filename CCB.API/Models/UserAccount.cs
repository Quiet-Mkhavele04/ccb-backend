using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class UserAccount
{
    public int UserAccountId { get; set; }

    public int ProfileId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; }

    public virtual Profile Profile { get; set; } = null!;
}
