namespace DistributedSis.application.DTOs
{
    public record CardRequestMessage(
        string UserId,
        string request
        );
    public record SaveBalanceRequest(
        string Merchant,
        decimal Amount
        );
    public record PaidCreditardRequest(
        string Merchant,
        decimal Amount
        );
    public record ActivateCardRequest(
        string UserId
        );
}
