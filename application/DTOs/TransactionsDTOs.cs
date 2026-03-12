namespace DistributedSis.application.DTOs
{
    public record PurchaseRequest
    (
        string Merchant,
        decimal Amount,
        string CardId
    );
}
