using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Settings;

public class PaymentSettings
{
    [MaxLength(3, ErrorMessage = "A maximum of 3 supported currencies can be configured")]
    public string[] SupportedCurrencies { get; set; } = [];
}
