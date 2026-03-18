using System.Net.Http.Json;

using PaymentGateway.External.Bank.Models;

namespace PaymentGateway.External.Bank.Services;

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;

    public BankClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/payments", request);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<BankPaymentResponse>();
    }
}
