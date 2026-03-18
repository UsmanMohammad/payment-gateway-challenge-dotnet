using System.Text.Json.Serialization;

namespace PaymentGateway.External.Bank.Models;

public class BankPaymentResponse
{
    [JsonPropertyName("authorized")]
    public bool Authorized { get; set; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; set; }
}
