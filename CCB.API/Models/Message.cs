using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Message
{
    public long MessageId { get; set; }

    // Kept for compatibility with the
    // current Contacts table.
    public int ContactId { get; set; }

    public int? SenderProfileId { get; set; }

    public int? RecipientProfileId { get; set; }

    public string MessageText { get; set; } =
        null!;

    // Kept temporarily for compatibility
    // with the original database design.
    public bool IsSent { get; set; }

    public bool Translated { get; set; }

    public DateTime SentAt { get; set; }

    // Null means the recipient has not
    // read the message yet.
    public DateTime? ReadAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual Contact Contact { get; set; } =
        null!;

    public virtual Profile? SenderProfile {
        get;
        set;
    }

    public virtual Profile? RecipientProfile {
        get;
        set;
    }

    public virtual ICollection<Report> Reports {
        get;
        set;
    } = new List<Report>();

    public virtual ICollection<Translation>
        Translations {
            get;
            set;
        } = new List<Translation>();
}