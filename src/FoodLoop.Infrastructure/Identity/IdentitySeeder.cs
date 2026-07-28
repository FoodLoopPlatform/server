using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoodLoop.Infrastructure.Identity;

/// <summary>
/// Ensures the RBAC roles (Customer, Merchant, Charity, Admin)
/// and the default administrator account exist on startup.
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
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentitySeeder");

        foreach (var roleName in AppRole.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(roleName));

                if (result.Succeeded)
                {
                    logger.LogInformation("Seeded role {RoleName}", roleName);
                }
                else
                {
                    logger.LogError(
                        "Failed to create role {RoleName}: {Errors}",
                        roleName,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentitySeeder");

        var options = services
            .GetRequiredService<IOptions<AdminUserOptions>>()
            .Value;

        var email = options.Email;
        var password = options.Password;
        var fullName = options.FullName;

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Admin user configuration is missing. Skipping admin seeding.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);

        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                Language = "en",
                Status = UserStatus.Active
            };

            var result = await userManager.CreateAsync(admin, password);

            if (!result.Succeeded)
            {
                logger.LogError(
                    "Failed to create admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));

                return;
            }

            await userManager.AddToRoleAsync(admin, AppRole.Admin);

            logger.LogInformation("Default administrator account created.");
        }
    }
}