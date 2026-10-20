using System.Security.Claims;
using CCB.API.Data;
using CCB.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private const decimal DeliveryFee = 35m;
    private const decimal FreeDeliveryMinimum = 500m;

    private readonly CCBDbContext _context;

    public OrdersController(
        CCBDbContext context
    )
    {
        _context = context;
    }

    /*
     * POST: /api/Orders/checkout
     */
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        CheckoutRequest request
    )
    {
        if (!TryGetCurrentProfileId(
            out var profileId
        ))
        {
            return Unauthorized(new
            {
                message =
                    "The access token does not contain a valid profile."
            });
        }

        if (
            request.Items == null ||
            request.Items.Count == 0
        )
        {
            return BadRequest(new
            {
                message =
                    "Your cart is empty."
            });
        }

        var fulfilmentType =
            request.FulfilmentType
                ?.Trim();

        if (
            fulfilmentType !=
                "Delivery" &&
            fulfilmentType !=
                "Collection"
        )
        {
            return BadRequest(new
            {
                message =
                    "Choose Delivery or Collection."
            });
        }

        var paymentMethod =
            request.PaymentMethod
                ?.Trim();

        if (
            paymentMethod !=
                "CCB Wallet" &&
            paymentMethod !=
                "Cash on Collection"
        )
        {
            return BadRequest(new
            {
                message =
                    "Choose a valid payment method."
            });
        }

        if (
            fulfilmentType ==
                "Delivery" &&
            string.IsNullOrWhiteSpace(
                request.DeliveryAddress
            )
        )
        {
            return BadRequest(new
            {
                message =
                    "Enter a delivery address."
            });
        }

        if (
            fulfilmentType ==
                "Delivery" &&
            paymentMethod ==
                "Cash on Collection"
        )
        {
            return BadRequest(new
            {
                message =
                    "Cash on Collection is only available for collection orders."
            });
        }

        var requestedItems =
            request.Items
                .Where(item =>
                    item.ProductId > 0 &&
                    item.Quantity > 0
                )
                .GroupBy(item =>
                    item.ProductId
                )
                .Select(group =>
                    new CheckoutItemRequest
                    {
                        ProductId =
                            group.Key,

                        Quantity =
                            group.Sum(item =>
                                item.Quantity
                            )
                    }
                )
                .ToList();

        if (
            requestedItems.Count == 0 ||
            requestedItems.Count !=
                request.Items
                    .Select(item =>
                        item.ProductId
                    )
                    .Distinct()
                    .Count()
        )
        {
            return BadRequest(new
            {
                message =
                    "One or more cart items are invalid."
            });
        }

        var productIds =
            requestedItems
                .Select(item =>
                    item.ProductId
                )
                .ToList();

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        try
        {
            var profile =
                await _context.Profiles
                    .FirstOrDefaultAsync(
                        item =>
                            item.ProfileId ==
                            profileId
                    );

            if (profile == null)
            {
                return NotFound(new
                {
                    message =
                        "Profile not found."
                });
            }

            var products =
                await _context.Products
                    .Where(product =>
                        productIds.Contains(
                            product.ProductId
                        )
                    )
                    .ToListAsync();

            if (
                products.Count !=
                productIds.Count
            )
            {
                return BadRequest(new
                {
                    message =
                        "One or more products no longer exist."
                });
            }

            decimal subtotal = 0;

            foreach (
                var requestedItem
                in requestedItems
            )
            {
                var product =
                    products.First(item =>
                        item.ProductId ==
                        requestedItem.ProductId
                    );

                if (!product.IsAvailable)
                {
                    return BadRequest(new
                    {
                        message =
                            $"{product.Name} is no longer available."
                    });
                }

                if (
                    product.StockQuantity <
                    requestedItem.Quantity
                )
                {
                    return BadRequest(new
                    {
                        message =
                            $"Only {product.StockQuantity} of {product.Name} are available."
                    });
                }

                subtotal +=
                    product.Price *
                    requestedItem.Quantity;
            }

            var calculatedDeliveryFee =
                fulfilmentType ==
                    "Delivery" &&
                subtotal <
                    FreeDeliveryMinimum
                    ? DeliveryFee
                    : 0m;

            var total =
                subtotal +
                calculatedDeliveryFee;

            if (
                paymentMethod ==
                    "CCB Wallet"
            )
            {
                if (
                    profile.Balance <
                    total
                )
                {
                    return BadRequest(new
                    {
                        message =
                            "Your CCB Wallet balance is too low for this order.",

                        required =
                            total,

                        available =
                            profile.Balance
                    });
                }

                profile.Balance -=
                    total;

                profile.UpdatedAt =
                    DateTime.UtcNow;
            }

            var order =
                new Order
                {
                    ProfileId =
                        profileId,

                    OrderNumber =
                        CreateOrderNumber(),

                    FulfilmentType =
                        fulfilmentType,

                    DeliveryAddress =
                        fulfilmentType ==
                            "Delivery"
                            ? request
                                .DeliveryAddress!
                                .Trim()
                            : null,

                    PaymentMethod =
                        paymentMethod,

                    Subtotal =
                        subtotal,

                    DeliveryFee =
                        calculatedDeliveryFee,

                    Total =
                        total,

                    Status =
                        paymentMethod ==
                            "CCB Wallet"
                            ? "Confirmed"
                            : "Pending",

                    CreatedAt =
                        DateTime.UtcNow
                };

            foreach (
                var requestedItem
                in requestedItems
            )
            {
                var product =
                    products.First(item =>
                        item.ProductId ==
                        requestedItem.ProductId
                    );

                var lineTotal =
                    product.Price *
                    requestedItem.Quantity;

                order.OrderItems.Add(
                    new OrderItem
                    {
                        ProductId =
                            product.ProductId,

                        ProductName =
                            product.Name,

                        UnitPrice =
                            product.Price,

                        Quantity =
                            requestedItem.Quantity,

                        LineTotal =
                            lineTotal
                    }
                );

                product.StockQuantity -=
                    requestedItem.Quantity;

                if (
                    product.StockQuantity ==
                    0
                )
                {
                    product.IsAvailable =
                        false;
                }

                product.UpdatedAt =
                    DateTime.UtcNow;
            }

            _context.Orders.Add(order);

            await _context
                .SaveChangesAsync();

            await transaction
                .CommitAsync();

            return Ok(new
            {
                message =
                    "Order placed successfully.",

                order = new
                {
                    orderId =
                        order.OrderId,

                    orderNumber =
                        order.OrderNumber,

                    fulfilmentType =
                        order.FulfilmentType,

                    deliveryAddress =
                        order.DeliveryAddress,

                    paymentMethod =
                        order.PaymentMethod,

                    subtotal =
                        order.Subtotal,

                    deliveryFee =
                        order.DeliveryFee,

                    total =
                        order.Total,

                    status =
                        order.Status,

                    createdAt =
                        order.CreatedAt,

                    itemCount =
                        order.OrderItems
                            .Sum(item =>
                                item.Quantity
                            )
                },

                walletBalance =
                    profile.Balance
            });
        }
        catch
        {
            await transaction
                .RollbackAsync();

            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                new
                {
                    message =
                        "The order could not be completed. Please try again."
                }
            );
        }
    }

    /*
     * GET: /api/Orders
     */
    [HttpGet]
    public async Task<IActionResult> GetMyOrders()
    {
        if (!TryGetCurrentProfileId(
            out var profileId
        ))
        {
            return Unauthorized(new
            {
                message =
                    "The access token does not contain a valid profile."
            });
        }

        var orders =
            await _context.Orders
                .AsNoTracking()
                .Where(order =>
                    order.ProfileId ==
                    profileId
                )
                .OrderByDescending(order =>
                    order.CreatedAt
                )
                .Select(order => new
                {
                    orderId =
                        order.OrderId,

                    orderNumber =
                        order.OrderNumber,

                    fulfilmentType =
                        order.FulfilmentType,

                    paymentMethod =
                        order.PaymentMethod,

                    total =
                        order.Total,

                    status =
                        order.Status,

                    createdAt =
                        order.CreatedAt,

                    itemCount =
                        order.OrderItems
                            .Sum(item =>
                                item.Quantity
                            )
                })
                .ToListAsync();

        return Ok(new
        {
            orders
        });
    }

    /*
     * GET: /api/Orders/5
     */
    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> GetOrder(
        int orderId
    )
    {
        if (!TryGetCurrentProfileId(
            out var profileId
        ))
        {
            return Unauthorized(new
            {
                message =
                    "The access token does not contain a valid profile."
            });
        }

        var order =
            await _context.Orders
                .AsNoTracking()
                .Where(item =>
                    item.OrderId ==
                        orderId &&
                    item.ProfileId ==
                        profileId
                )
                .Select(item => new
                {
                    orderId =
                        item.OrderId,

                    orderNumber =
                        item.OrderNumber,

                    fulfilmentType =
                        item.FulfilmentType,

                    deliveryAddress =
                        item.DeliveryAddress,

                    paymentMethod =
                        item.PaymentMethod,

                    subtotal =
                        item.Subtotal,

                    deliveryFee =
                        item.DeliveryFee,

                    total =
                        item.Total,

                    status =
                        item.Status,

                    createdAt =
                        item.CreatedAt,

                    items =
                        item.OrderItems
                            .Select(orderItem =>
                                new
                                {
                                    orderItemId =
                                        orderItem
                                            .OrderItemId,

                                    productId =
                                        orderItem
                                            .ProductId,

                                    productName =
                                        orderItem
                                            .ProductName,

                                    unitPrice =
                                        orderItem
                                            .UnitPrice,

                                    quantity =
                                        orderItem
                                            .Quantity,

                                    lineTotal =
                                        orderItem
                                            .LineTotal
                                }
                            )
                            .ToList()
                })
                .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(new
            {
                message =
                    "Order not found."
            });
        }

        return Ok(new
        {
            order
        });
    }

    private bool TryGetCurrentProfileId(
        out int profileId
    )
    {
        var profileIdValue =
            User.FindFirstValue(
                "profileId"
            );

        return int.TryParse(
            profileIdValue,
            out profileId
        );
    }

    private static string CreateOrderNumber()
    {
        var shortId =
            Guid.NewGuid()
                .ToString("N")
                [..8]
                .ToUpperInvariant();

        return
            $"CCB-{DateTime.UtcNow:yyyyMMdd}-{shortId}";
    }
}

public class CheckoutRequest
{
    public string? FulfilmentType { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? PaymentMethod { get; set; }

    public List<CheckoutItemRequest> Items { get; set; }
        = new();
}

public class CheckoutItemRequest
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
}