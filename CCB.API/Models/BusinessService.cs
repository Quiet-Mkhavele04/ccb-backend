using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class BusinessService
{
    public int BusinessServiceId { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public decimal? Price { get; set; }

    public string? Description { get; set; }

    public virtual Business Business { get; set; } = null!;
}
