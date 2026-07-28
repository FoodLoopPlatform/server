# FoodLoop tests

Unit tests live here, mirroring `src/` one project per layer:

```
tests/
  Directory.Build.props          shared settings for every test project below
  FoodLoop.Domain.Tests/         pure entity/logic tests, zero mocking
  FoodLoop.Application.Tests/    Application-layer types (Result, validators, etc.)
  FoodLoop.Infrastructure.Tests/ service tests: Moq for collaborators, EF Core
                                  InMemory for ApplicationDbContext
```

## Why per-layer projects instead of one big test project

- Test failures point straight at the layer that broke.
- Domain and Application tests stay dependency-free (no EF Core, no ASP.NET Identity),
  so they run in milliseconds and can't accidentally start depending on infrastructure
  concerns.
- Mirrors `src/`, so it's obvious where a new test for `FoodLoop.X/Foo.cs` belongs:
  `tests/FoodLoop.X.Tests/Foo.cs`.

## Stack

- **xUnit** — test framework.
- **FluentAssertions 6.12.1** — pinned deliberately: versions 7+ moved to a commercial
  license for for-profit use. 6.12.1 is the last MIT-licensed release and has everything
  this project needs. If you already hold a Xceed license, feel free to bump it; otherwise
  leave it pinned (or swap to the MIT-licensed `AwesomeAssertions` fork, a drop-in
  replacement with the same API).
- **Moq** — mocking collaborators (`IEmailService`, `IFileStorageService`,
  `IJwtTokenService`, `UserManager<ApplicationUser>`, ...).
- **Microsoft.EntityFrameworkCore.InMemory** — used instead of mocking `DbContext`/`DbSet`
  directly. `AuthService` depends on the concrete `ApplicationDbContext` (not the
  `IApplicationDbContext` abstraction), and several services rely on real LINQ-to-EF
  query behaviour, so InMemory gives closer-to-real coverage than hand-rolled mocks would.
  See `TestSupport/ApplicationDbContextFactory.cs` — every test gets its own uniquely
  named database, so tests never leak state into each other.
- **coverlet.collector** — code coverage collection for `dotnet test --collect`.

## Conventions

- **Naming**: `MethodName_should_<expected behavior>_when_<condition>`.
- **Arrange / Act / Assert**, separated by a blank line, no comments needed to label them.
- One behavior per test. Prefer several small `[Fact]`s (or a `[Theory]` with
  `[InlineData]`) over one test asserting many unrelated things.
- Mock only what you don't own: interfaces and framework types you depend on
  (`IEmailService`, `UserManager<T>`), not your own DTOs or entities.
- `UserManager<TUser>` has no interface — see `TestSupport/MockUserManagerFactory.cs`
  for the standard workaround (mock the class directly; its members are virtual).

## Running

```bash
dotnet restore
dotnet test
```

Run one project only:

```bash
dotnet test tests/FoodLoop.Infrastructure.Tests
```

With coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Adding a new test

1. Find (or create) the matching `*.Tests` project for the layer you're testing.
2. Mirror the folder path of the file under test (e.g. a class in
   `src/FoodLoop.Infrastructure/Services/Foo.cs` gets tests in
   `tests/FoodLoop.Infrastructure.Tests/Services/FooTests.cs`).
3. If the class under test needs `ApplicationDbContext`, use
   `ApplicationDbContextFactory.Create()`. If it needs `UserManager<ApplicationUser>`,
   use `MockUserManagerFactory.Create()`. Don't build these by hand in each test file.
