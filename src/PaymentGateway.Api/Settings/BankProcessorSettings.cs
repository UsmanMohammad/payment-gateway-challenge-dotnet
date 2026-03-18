using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Settings;

public class BankProcessorSettings
{
    [Required]
    public string BaseUrl { get; set; } = string.Empty;
}
