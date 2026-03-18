using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.External.Bank.Models;
using PaymentGateway.External.Bank.Services;

namespace PaymentGateway.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly PaymentsRepository _repository;
    private readonly IBankClient _bankClient;

    public PaymentService(PaymentsRepository repository, IBankClient bankClient)
    {
        _repository = repository;
        _bankClient = bankClient;
    }

    public async Task<PostPaymentResponse?> ProcessAsync(PostPaymentRequest request)
    {
        var bankResponse = await _bankClient.ProcessPaymentAsync(new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        });

        if (bankResponse is null)
            return null;

        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };

        _repository.Add(payment);

        return payment;
    }

    public async Task<PostPaymentResponse?> GetByIdAsync(Guid id) =>
        await _repository.GetAsync(id);
}
