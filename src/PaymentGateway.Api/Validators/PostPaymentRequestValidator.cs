using FluentValidation;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Settings;

namespace PaymentGateway.Api.Validators;

public class PostPaymentRequestValidator : AbstractValidator<PostPaymentRequest>
{
    public PostPaymentRequestValidator(IOptions<PaymentSettings> options)
    {
        var allowedCurrencies = options.Value.SupportedCurrencies;
        var allowedCurrenciesMessage = string.Join(", ", allowedCurrencies);
        RuleFor(x => x.CardNumber)
            .NotEmpty().WithMessage("Card number is required")
            .Length(14, 19).WithMessage("Card number must be between 14 and 19 characters")
            .Matches(@"^\d+$").WithMessage("Card number must only contain numeric characters");

        RuleFor(x => x.ExpiryMonth)
            .InclusiveBetween(1, 12).WithMessage("Expiry month must be between 1 and 12");

        RuleFor(x => x.ExpiryYear)
            .Must((request, year) =>
            {
                var now = DateTime.UtcNow;
                return year > now.Year || (year == now.Year && request.ExpiryMonth >= now.Month);
            })
            .WithMessage("Card has expired");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be 3 characters")
            .Must(c => allowedCurrencies.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Currency must be one of: {allowedCurrenciesMessage}");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be a positive integer");

        RuleFor(x => x.Cvv)
            .NotEmpty().WithMessage("CVV is required")
            .Length(3, 4).WithMessage("CVV must be 3-4 characters")
            .Matches(@"^\d+$").WithMessage("CVV must only contain numeric characters");
    }
}
