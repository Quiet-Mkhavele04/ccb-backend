using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class BusinessOwner
{
    public int BusinessOwnerId { get; set; }

    public int BusinessId { get; set; }

    public int ProfileId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Business Business { get; set; } = null!;

    public virtual Profile Profile { get; set; } = null!;
}
