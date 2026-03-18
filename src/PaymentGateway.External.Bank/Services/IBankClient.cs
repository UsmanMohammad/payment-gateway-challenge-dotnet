using PaymentGateway.External.Bank.Models;

namespace PaymentGateway.External.Bank.Services;

public interface IBankClient
{
    Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request);
}
