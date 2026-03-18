using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests;

/// <summary>
/// Integration tests that run against the live bank simulator.
/// Requires: docker-compose up
/// Run with: dotnet test --filter "Category=Integration"
///
/// Bank simulator card number rules (based on last digit):
///   1, 3, 5, 7, 9 - 200 Authorized
///   2, 4, 6, 8     - 200 Declined
///   0              - 503 Service Unavailable - API returns 502
/// </summary>
[Trait("Category", "Integration")]
public class PaymentsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    private const string CardPrefix            = "42409927350671";
    private const string AuthorizedCardNumber  = "424099273506717"; // ends in 7 (odd) - Authorized
    private const string DeclinedCardNumber    = "424099273506712"; // ends in 2 (even) - Declined
    private const string UnavailableCardNumber = "424099273506710"; // ends in 0 (zero) - 503

    public PaymentsControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static PostPaymentRequest ValidRequest(string cardNumber) => new()
    {
        CardNumber = cardNumber,
        ExpiryMonth = 4,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "USD",
        Amount = 1337,
        Cvv = "123"
    };

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(9)]
    public async Task AuthorizesPaymentWhenCardEndsInOddDigit(int lastDigit)
    {
        // Arrange
        var request = ValidRequest($"{CardPrefix}{lastDigit}");

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal($"671{lastDigit}", payment.CardNumberLastFour);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal(1337, payment.Amount);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(DateTime.UtcNow.Year + 1, payment.ExpiryYear);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public async Task DeclinesPaymentWhenCardEndsInEvenDigit(int lastDigit)
    {
        // Arrange
        var request = ValidRequest($"{CardPrefix}{lastDigit}");

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal($"671{lastDigit}", payment.CardNumberLastFour);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal(1337, payment.Amount);
    }

    [Fact]
    public async Task Returns502WhenBankSimulatorIsUnavailable()
    {
        // Arrange
        var request = ValidRequest(UnavailableCardNumber);

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizedPaymentIsStoredAndRetrievableById()
    {
        // Arrange
        var request = ValidRequest(AuthorizedCardNumber);

        // Act
        var postResponse = await _client.PostAsJsonAsync("/api/Payments", request);
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/Payments/{created.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(PaymentStatus.Authorized, retrieved.Status);
        Assert.Equal(created.CardNumberLastFour, retrieved.CardNumberLastFour);
        Assert.Equal(created.Currency, retrieved.Currency);
        Assert.Equal(created.Amount, retrieved.Amount);
        Assert.Equal(created.ExpiryMonth, retrieved.ExpiryMonth);
        Assert.Equal(created.ExpiryYear, retrieved.ExpiryYear);
    }

    [Fact]
    public async Task DeclinedPaymentIsStoredAndRetrievableById()
    {
        // Arrange
        var request = ValidRequest(DeclinedCardNumber);

        // Act
        var postResponse = await _client.PostAsJsonAsync("/api/Payments", request);
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/Payments/{created.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(PaymentStatus.Declined, retrieved.Status);
    }

    [Fact]
    public async Task UnavailablePaymentIsNotStored()
    {
        // Arrange — a bank 503 should not result in a stored payment
        var request = ValidRequest(UnavailableCardNumber);

        // Act
        var postResponse = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert — 502 returned and no payment ID was issued
        Assert.Equal(HttpStatusCode.BadGateway, postResponse.StatusCode);
        Assert.Equal(Guid.Empty, payment?.Id ?? Guid.Empty);
    }

    [Theory]
    [InlineData("USD", 1337)] 
    [InlineData("EUR", 1337)] 
    [InlineData("JPY", 1337)]   
    public async Task AuthorizesPaymentAcrossSupportedCurrencies(string currency, int amount)
    {
        // Arrange
        var request = ValidRequest(AuthorizedCardNumber);
        request.Currency = currency;
        request.Amount = amount;

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(currency, payment.Currency);
        Assert.Equal(amount, payment.Amount);
    }

    [Theory]
    [InlineData("USD", 1)]      
    [InlineData("EUR", 1)] 
    [InlineData("JPY", 1)]
    public async Task DeclinesPaymentAcrossSupportedCurrencies(string currency, int amount)
    {
        // Arrange
        var request = ValidRequest(DeclinedCardNumber);
        request.Currency = currency;
        request.Amount = amount;

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal(currency, payment.Currency);
        Assert.Equal(amount, payment.Amount);
    }

    [Fact]
    public async Task FullCardNumberIsNeverReturnedInPostResponse()
    {
        // Arrange
        var request = ValidRequest(AuthorizedCardNumber);

        // Act
        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var raw = await response.Content.ReadAsStringAsync();

        // Assert — the full card number must not appear anywhere in the response body
        Assert.DoesNotContain(AuthorizedCardNumber, raw);
    }

    [Fact]
    public async Task FullCardNumberIsNeverReturnedInGetResponse()
    {
        // Arrange
        var postResponse = await _client.PostAsJsonAsync("/api/Payments", ValidRequest(AuthorizedCardNumber));
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.NotNull(created);

        // Act
        var getResponse = await _client.GetAsync($"/api/Payments/{created.Id}");
        var raw = await getResponse.Content.ReadAsStringAsync();

        // Assert
        Assert.DoesNotContain(AuthorizedCardNumber, raw);
    }
}
