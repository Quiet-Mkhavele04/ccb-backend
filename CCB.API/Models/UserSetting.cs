using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class UserSetting
{
    public int UserSettingId { get; set; }

    public int ProfileId { get; set; }

    public bool NotificationsEnabled { get; set; }

    public bool ReadReceiptsEnabled { get; set; }

    public bool TranslationEnabled { get; set; }

    public bool DarkModeEnabled { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Profile Profile { get; set; } = null!;
}
