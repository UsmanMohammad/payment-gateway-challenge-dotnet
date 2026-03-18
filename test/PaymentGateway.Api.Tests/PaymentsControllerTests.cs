using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.External.Bank.Models;
using PaymentGateway.External.Bank.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private readonly Random _random = new();

    private class StubBankClient : IBankClient
    {
        private readonly BankPaymentResponse? _response;
        public StubBankClient(BankPaymentResponse? response) => _response = response;
        public Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request) =>
            Task.FromResult(_response);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IBankClient? bankClient = null,
        PaymentsRepository? repository = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    if (repository is not null)
                        services.AddSingleton(repository);
                    if (bankClient is not null)
                        services.AddSingleton(bankClient);
                }));
    }

    private PostPaymentRequest ValidRequest(string cardNumber = "4240992735067177") => new()
    {
        CardNumber = cardNumber,
        ExpiryMonth = 4,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "USD",
        Amount = 100,
        Cvv = "123"
    };

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            ExpiryYear = _random.Next(2027, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999).ToString(),
            Currency = "USD"
        };

        var repository = new PaymentsRepository();
        repository.Add(payment);

        var client = CreateFactory(repository: repository).CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse.Id);
        Assert.Equal(payment.CardNumberLastFour, paymentResponse.CardNumberLastFour);
        Assert.Equal(payment.Currency, paymentResponse.Currency);
        Assert.Equal(payment.Amount, paymentResponse.Amount);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        var client = CreateFactory().CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProcessesAnAuthorizedPayment()
    {
        // Arrange
        var bankStub = new StubBankClient(new BankPaymentResponse
        {
            Authorized = true,
            AuthorizationCode = Guid.NewGuid().ToString()
        });

        var client = CreateFactory(bankClient: bankStub).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("7177", payment.CardNumberLastFour);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal(100, payment.Amount);
    }

    [Fact]
    public async Task ProcessesADeclinedPayment()
    {
        // Arrange
        var bankStub = new StubBankClient(new BankPaymentResponse { Authorized = false });

        var client = CreateFactory(bankClient: bankStub).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest("4240992735067172"));
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal("7172", payment.CardNumberLastFour);
    }

    [Fact]
    public async Task AuthorizedPaymentCanBeRetrieved()
    {
        // Arrange
        var bankStub = new StubBankClient(new BankPaymentResponse { Authorized = true });
        var client = CreateFactory(bankClient: bankStub).CreateClient();

        var postResponse = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.NotNull(created);

        // Act
        var getResponse = await client.GetAsync($"/api/Payments/{created.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(created.Status, retrieved.Status);
        Assert.Equal(created.CardNumberLastFour, retrieved.CardNumberLastFour);
    }

    [Theory]
    [InlineData(1, "USD")]      
    [InlineData(1050, "USD")]  
    [InlineData(1, "EUR")]      
    [InlineData(1050, "EUR")]   
    [InlineData(1, "JPY")]      
    [InlineData(1050, "JPY")]
    public async Task HandlesCurrenciesWithAndWithOutMinorUnits(int amount, string currency)
    {
        // Arrange
        var bankStub = new StubBankClient(new BankPaymentResponse { Authorized = true });
        var client = CreateFactory(bankClient: bankStub).CreateClient();

        var request = ValidRequest();
        request.Currency = currency;
        request.Amount = amount;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var postPaymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(postPaymentResponse);
        Assert.Equal(currency, postPaymentResponse.Currency);
        Assert.Equal(amount, postPaymentResponse.Amount);
    }
    
    [Fact]
    public async Task Returns502WhenBankUnavailable()
    {
        // Arrange
        var bankStub = new StubBankClient(null);
        var client = CreateFactory(bankClient: bankStub).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", ValidRequest());

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]                   // too short
    [InlineData("12345678901234567890")]  // too long (20 chars)
    [InlineData("222240534324887A")]      // non-numeric
    public async Task RejectsInvalidCardNumber(string cardNumber)
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task RejectsInvalidExpiryMonth(int month)
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.ExpiryMonth = month;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectsExpiredCard()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year - 1;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]    // too short
    [InlineData("USDX")]  // too long
    [InlineData("GBP")]   // not in allowed list
    public async Task RejectsInvalidCurrency(string currency)
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.Currency = currency;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RejectsInvalidAmount(int amount)
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.Amount = amount;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]    // too short
    [InlineData("12345")] // too long
    [InlineData("12A")]   // non-numeric
    public async Task RejectsInvalidCvv(string cvv)
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.Cvv = cvv;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RejectedResponseContainsStatusAndErrors()
    {
        // Arrange
        var client = CreateFactory().CreateClient();
        var request = ValidRequest();
        request.CardNumber = "invalid";

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var body = await response.Content.ReadFromJsonAsync<RejectedPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Rejected.ToString(), body.Status);
        Assert.NotEmpty(body.Errors);
    }
}
