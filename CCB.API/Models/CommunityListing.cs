using System;

namespace CCB.API.Models;

public partial class CommunityListing
{
    public int CommunityListingId {
        get;
        set;
    }

    public int CommunityId {
        get;
        set;
    }

    public int? SellerProfileId {
        get;
        set;
    }

    public string Title {
        get;
        set;
    } = null!;

    public decimal? Price {
        get;
        set;
    }

    public string? Description {
        get;
        set;
    }

    public string? Category {
        get;
        set;
    }

    public string? Condition {
        get;
        set;
    }

    public string? Location {
        get;
        set;
    }

    public string? Contact {
        get;
        set;
    }

    public string? ImageUrl {
        get;
        set;
    }

    public bool IsAvailable {
        get;
        set;
    }

    public DateTime CreatedAt {
        get;
        set;
    }

    public DateTime? UpdatedAt {
        get;
        set;
    }

    public virtual Community Community {
        get;
        set;
    } = null!;

    public virtual Profile? SellerProfile {
        get;
        set;
    }
}