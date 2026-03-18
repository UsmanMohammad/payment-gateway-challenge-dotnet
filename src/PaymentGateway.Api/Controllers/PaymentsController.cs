using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostPaymentResponse>> GetPaymentAsync(Guid id)
    {
        var payment = await _paymentService.GetByIdAsync(id);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> CreatePaymentAsync([FromBody] PostPaymentRequest request)
    {
        var payment = await _paymentService.ProcessAsync(request);
        return payment is null ? StatusCode(502, new { error = "Bank processor unavailable" }) : Ok(payment);
    }
}
