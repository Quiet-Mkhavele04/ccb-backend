namespace CCB.API.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int ProfileId { get; set; }

    public string OrderNumber { get; set; } = null!;

    public string FulfilmentType { get; set; } = null!;

    public string? DeliveryAddress { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public decimal Subtotal { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal Total { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Profile Profile { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; }
        = new List<OrderItem>();
}