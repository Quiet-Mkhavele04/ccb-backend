using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class BlockedContact
{
    public int BlockedContactId { get; set; }

    public int ProfileId { get; set; }

    public int ContactId { get; set; }

    public DateTime BlockedAt { get; set; }

    public virtual Contact Contact { get; set; } = null!;

    public virtual Profile Profile { get; set; } = null!;
}
