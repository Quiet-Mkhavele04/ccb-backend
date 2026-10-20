namespace CCB.API.Services.Payments;

public sealed record PaymentProviderTransferRequest(
    int SenderProfileId,
    int RecipientProfileId,
    decimal Amount,
    string? Reference
);

public sealed record PaymentProviderTransferResult(
    bool Success,
    string Status,
    string? ProviderReference,
    string? ErrorMessage
);

public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<PaymentProviderTransferResult>
        ProcessTransferAsync(
            PaymentProviderTransferRequest request,
            CancellationToken cancellationToken =
                default
        );
}