using System;

namespace CCB.API.Models;

public partial class CommunityJob
{
    public int CommunityJobId {
        get;
        set;
    }

    public int CommunityId {
        get;
        set;
    }

    public int? PostedByProfileId {
        get;
        set;
    }

    public string Title {
        get;
        set;
    } = null!;

    public string Detail {
        get;
        set;
    } = null!;

    public string? Company {
        get;
        set;
    }

    public string? Location {
        get;
        set;
    }

    public string? EmploymentType {
        get;
        set;
    }

    public string? ApplicationContact {
        get;
        set;
    }

    public DateTime PostedAt {
        get;
        set;
    }

    public DateTime? ExpiresAt {
        get;
        set;
    }

    public bool IsActive {
        get;
        set;
    }

    public virtual Community Community {
        get;
        set;
    } = null!;

    public virtual Profile? PostedByProfile {
        get;
        set;
    }
}