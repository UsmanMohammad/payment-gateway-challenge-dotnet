# Solution notes

## Running the project

```bash
# Start the bank simulator (required for integration tests)
docker-compose up

# Unit tests only — no Docker needed
dotnet test --filter "Category!=Integration"

# Integration tests — requires docker-compose up
dotnet test --filter "Category=Integration"

# All tests
dotnet test

# Run the API
dotnet run --project src/PaymentGateway.Api
```

---

## Design considerations and assumptions

### Validation

I used FluentValidation for validation as the rules can be invoked before the controller actions are run.


Validation rules:
- Card number: 14–19 numeric characters
- Expiry: month 1–12, and the month+year combination must be in the future — a card expiring in March of this year is still valid in January, so you can't just check the year alone
- Currency: exactly 3 characters, must be one of the configured supported currencies
- Amount: positive integer, anything ≤ 0 is rejected
- CVV: 3–4 numeric characters

---

### Currencies come from config, not hardcoded

The initial implementation had `USD`, `EUR`, `GBP` baked into the validator. Moving them to `appsettings.json` under `Payment:SupportedCurrencies` means you can add or remove currencies without touching code. The validator receives them via `IOptions<PaymentSettings>` and builds the error message dynamically.

There's also a startup check — if someone configures more than 3 currencies, the app fails immediately with a clear error rather than silently accepting the bad config at runtime.

GBP was swapped out for JPY because it has no minor unit (no pence/cents equivalent).

As this is the payment gateway, there isn't any input conversion for decimal currencies as that is done by the merchant calling the endpoint.
The inclusion of JPY alongside EUR and USD was to demonstrate the solution can minor and non minor currencies
---

### BankProcessor settings use IOptions with startup validation

`BankProcessor:BaseUrl` is required. Rather than null-checking it in `BankClient` at request time and getting a confusing `NullReferenceException`, startup validation catches it immediately:

```csharp
builder.Services.AddOptions<BankProcessorSettings>()
    .BindConfiguration("BankProcessor")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

The `[Required]` annotation on `BaseUrl` means a missing config causes the app to fail fast with a readable error on boot, not partway through the first real payment request.

---

### PaymentGateway.External.Bank is a separate project

The bank-related types (`IBankClient`, `BankClient`, `BankPaymentRequest`, `BankPaymentResponse`) live in a separate `PaymentGateway.External.Bank` project. The reasoning:

- It keeps the core API project from knowing too much about the bank's wire format
- The bank models use `[JsonPropertyName]` snake_case attributes that are an implementation detail of the simulator — those don't belong alongside the API's own models
- If the bank integration ever needed to be swapped out or versioned, there's a clear boundary

---

### PaymentService layer

The controller used to do everything: validate, call the bank, map the response, store it. Extracting a `PaymentService` makes the controller a thin HTTP adapter:


The controller only decides what HTTP status code to return. All the payment logic is in `PaymentService`, which has no HTTP concerns.

---

### Two test suites

There are two test files:

**`PaymentsControllerTests.cs`** — unit tests. Use `StubBankClient` as a mocked client

**`PaymentsControllerIntegrationTests.cs`** — integration tests requires docker-compose up. Tagged `[Trait("Category", "Integration")]` so they can be excluded from a command line run.

---

### What I kept simple on purpose

No real database — the in-memory data store.

No DDD or seperate architecture for core, common, presentation and infrastructure layers.

No logging, opentelemetry, no polly resiliency.