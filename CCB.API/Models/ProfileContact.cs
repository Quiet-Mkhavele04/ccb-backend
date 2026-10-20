using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class ProfileContact
{
    public int ProfileContactId { get; set; }

    public int ProfileId { get; set; }

    public int ContactId { get; set; }

    public DateTime AddedAt { get; set; }

    public virtual Contact Contact { get; set; } = null!;

    public virtual Profile Profile { get; set; } = null!;
}
