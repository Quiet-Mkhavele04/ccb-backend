using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Business
{
    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public decimal? Rating { get; set; }

    public string? BannerColor { get; set; }

    public string? BannerImageUrl { get; set; }

    public string? Description { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public bool Featured { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<BusinessOwner> BusinessOwners { get; set; } = new List<BusinessOwner>();

    public virtual ICollection<BusinessService> BusinessServices { get; set; } = new List<BusinessService>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
