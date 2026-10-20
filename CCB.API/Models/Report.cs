using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Report
{
    public long ReportId { get; set; }

    public int ProfileId { get; set; }

    public int? ContactId { get; set; }

    public long? MessageId { get; set; }

    public string Reason { get; set; } = null!;

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Contact? Contact { get; set; }

    public virtual Message? Message { get; set; }

    public virtual Profile Profile { get; set; } = null!;
}
