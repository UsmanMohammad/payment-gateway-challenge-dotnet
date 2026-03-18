using FluentValidation;
using FluentValidation.AspNetCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Settings;
using PaymentGateway.Api.Validators;
using PaymentGateway.External.Bank.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage);

        return new BadRequestObjectResult(new RejectedPaymentResponse { Errors = errors });
    };
});

builder.Services.AddOptions<BankProcessorSettings>()
    .BindConfiguration("BankProcessor")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<PaymentSettings>()
    .BindConfiguration("Payment")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<PostPaymentRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<PaymentsRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

builder.Services.AddHttpClient<IBankClient, BankClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        var settings = sp.GetRequiredService<IOptions<BankProcessorSettings>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl);
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

namespace PaymentGateway.Api
{
    public partial class Program { }
}
