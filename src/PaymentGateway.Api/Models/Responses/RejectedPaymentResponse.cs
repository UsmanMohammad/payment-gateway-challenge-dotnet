using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Models.Responses;

public class RejectedPaymentResponse
{
    public string Status => PaymentStatus.Rejected.ToString();
    public IEnumerable<string> Errors { get; init; } = [];
}
