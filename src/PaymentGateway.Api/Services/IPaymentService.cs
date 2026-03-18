using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentService
{
    Task<PostPaymentResponse?> ProcessAsync(PostPaymentRequest request);
    Task<PostPaymentResponse?> GetByIdAsync(Guid id);
}
