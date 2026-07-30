using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;

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
        await SeedCategoriesAsync(services);
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

    private static async Task SeedCategoriesAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        if (!context.Categories.Any())
        {
            var categories = new List<Category>
            {
                new() { Id = Guid.NewGuid(), Name = "Fruits & Vegetables", NameAr = "خضروات وفواكه" },
                new() { Id = Guid.NewGuid(), Name = "Bakery", NameAr = "مخبوزات" },
                new() { Id = Guid.NewGuid(), Name = "Dairy & Eggs", NameAr = "ألبان وبيض" },
                new() { Id = Guid.NewGuid(), Name = "Meals", NameAr = "وجبات جاهزة" },
                new() { Id = Guid.NewGuid(), Name = "Groceries", NameAr = "مواد غذائية" },
                new() { Id = Guid.NewGuid(), Name = "Desserts", NameAr = "حلويات" }
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded standard product categories.");
        }
    }
}