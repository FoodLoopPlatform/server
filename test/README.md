# FoodLoop Automated Test Suite

Unit and integration tests live in the `test/` directory, mirroring `src/` with one project per layer:

```
test/
  Directory.Build.props          shared settings for every test project below
  FoodLoop.Domain.Tests/         pure entity/logic tests, zero mocking
  FoodLoop.Application.Tests/    Application-layer types (DTO serialization, Result, commands)
  FoodLoop.Infrastructure.Tests/ Service tests, CQRS handler integration tests, AI pipelines,
                                 Polly resilience tests, SignalR, SQLite/EF InMemory
```

---

## 📊 Test Suite Summary

```text
========================================================================================
  Test Project                      Total Tests   Passed   Failed   Skipped   Pass Rate
========================================================================================
  FoodLoop.Domain.Tests                 28          28        0        0        100%
  FoodLoop.Application.Tests            11          11        0        0        100%
  FoodLoop.Infrastructure.Tests        451         451        0        0        100%
========================================================================================
  TOTAL                                490         490        0        0        100%
========================================================================================
```

---

## 🛠️ Stack & Libraries

- **xUnit** — test framework.
- **FluentAssertions 6.12.1** — assertion library.
- **Moq** — mocking collaborators (`IEmailService`, `IFileStorageService`, `IJwtTokenService`, `UserManager<ApplicationUser>`, `ResiliencePipelineProvider<string>`).
- **Microsoft.EntityFrameworkCore.InMemory** / **SQLite** — in-memory database testing.
- **Polly v8** — resilience pipeline test coverage (retries, timeouts, circuit breakers).
- **coverlet.collector** — code coverage collection.

---

## ▶️ Running Tests

Run all tests across the entire solution:
```bash
dotnet test
```

Run a specific project:
```bash
dotnet test test/FoodLoop.Infrastructure.Tests
```

Run with code coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

Run live remote AI microservice smoke tests:
```bash
dotnet test --filter "Category=LiveAiIntegration"
```
