using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Identity;

/// <summary>
/// Seeds the minimum required data at startup:
///   1. RBAC roles (Customer, Merchant, Charity, Admin)
///   2. Default administrator account
/// All other data (categories, products, stores, etc.) is managed via API or migrations.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await SeedRolesAsync(services);
        await SeedAdminAsync(services);
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        foreach (var roleName in AppRole.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(roleName));
                if (result.Succeeded)
                    logger.LogInformation("Seeded role {RoleName}", roleName);
                else
                    logger.LogError("Failed to create role {RoleName}: {Errors}",
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        var options = services.GetRequiredService<IOptions<AdminUserOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning("Admin user configuration is missing. Skipping admin seeding.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(options.Email);
        if (admin != null)
            return;

        admin = new ApplicationUser
        {
            UserName = options.Email,
            Email = options.Email,
            EmailConfirmed = true,
            FullName = options.FullName,
            Language = "ar",
            Status = UserStatus.Active
        };

        var createResult = await userManager.CreateAsync(admin, options.Password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, AppRole.Admin);
        logger.LogInformation("Default administrator account created.");
    }
}
