namespace CCB.API.Services.Payments;

public sealed class DemoPaymentProvider
    : IPaymentProvider
{
    public string ProviderName =>
        "CCB Demo Wallet";

    public Task<PaymentProviderTransferResult>
        ProcessTransferAsync(
            PaymentProviderTransferRequest request,
            CancellationToken cancellationToken =
                default
        )
    {
        if (request.Amount <= 0)
        {
            return Task.FromResult(
                new PaymentProviderTransferResult(
                    false,
                    "Failed",
                    null,
                    "The transfer amount must be greater than zero."
                )
            );
        }

        var providerReference =
            $"DEMO-{Guid.NewGuid():N}"
                .ToUpperInvariant();

        return Task.FromResult(
            new PaymentProviderTransferResult(
                true,
                "Completed",
                providerReference,
                null
            )
        );
    }
}