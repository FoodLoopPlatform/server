using FoodLoop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FoodLoop.Infrastructure.Tests.TestSupport;

/// <summary>
/// Creates a fresh InMemory-backed ApplicationDbContext for a single test.
///
/// Why InMemory instead of mocking IApplicationDbContext/DbSet directly: several
/// services (AuthService in particular) depend on the concrete ApplicationDbContext
/// and lean on real EF query behaviour (Where/FirstOrDefaultAsync, SaveChangesAsync
/// side effects like the audit-stamping override). InMemory gives us that behaviour
/// without a real database. Each call here uses a new, uniquely named database, so
/// tests never leak state into one another and can run in parallel safely.
/// </summary>
public static class ApplicationDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }
}
