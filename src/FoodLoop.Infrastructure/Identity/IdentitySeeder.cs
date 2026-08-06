using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        await SeedStoresAndProductsAsync(services);
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

        var standardCategories = new List<(string Name, string NameAr)>
        {
            ("Fruits & Vegetables", "خضروات وفواكه"),
            ("Bakery", "مخبوزات"),
            ("Dairy & Eggs", "ألبان وبيض"),
            ("Meals", "وجبات جاهزة"),
            ("Groceries", "مواد غذائية"),
            ("Desserts", "حلويات")
        };

        var addedAny = false;
        foreach (var std in standardCategories)
        {
            if (!context.Categories.Any(c => c.Name == std.Name))
            {
                context.Categories.Add(new Category
                {
                    Id = Guid.NewGuid(),
                    Name = std.Name,
                    NameAr = std.NameAr
                });
                addedAny = true;
            }
        }

        if (addedAny)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded missing standard product categories.");
        }
    }

    private static async Task SeedStoresAndProductsAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        // Seed Spinneys Merchant
        var spinneysEmail = "merchant.spinneys@example.com";
        var spinneysUser = await userManager.FindByEmailAsync(spinneysEmail);
        if (spinneysUser == null)
        {
            spinneysUser = new ApplicationUser
            {
                UserName = spinneysEmail,
                Email = spinneysEmail,
                EmailConfirmed = true,
                FullName = "Spinneys Merchant",
                Status = UserStatus.Active
            };
            await userManager.CreateAsync(spinneysUser, "Password@123");
            await userManager.AddToRoleAsync(spinneysUser, AppRole.Merchant);

            var store = new Store
            {
                Id = Guid.NewGuid(),
                OwnerId = spinneysUser.Id,
                Name = "Spinneys Supermarket",
                VerificationStatus = VerificationStatus.Verified,
                Latitude = 30.0444,
                Longitude = 31.2357,
                City = "Cairo",
                Neighborhood = "Maadi",
                Street = "Road 9",
                BuildingNo = "24",
                AverageRating = 4.5
            };
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            var fruitsCategory = context.Categories.FirstOrDefault(c => c.Name == "Fruits & Vegetables");
            var bakeryCategory = context.Categories.FirstOrDefault(c => c.Name == "Bakery");
            var dairyCategory = context.Categories.FirstOrDefault(c => c.Name == "Dairy & Eggs");

            if (fruitsCategory != null)
            {
                context.Products.Add(new Product
                {
                    StoreId = store.Id,
                    CategoryId = fruitsCategory.Id,
                    Title = "Organic Bananas",
                    TitleAr = "موز عضوي",
                    Description = "Sweet import organic bananas",
                    OriginalPrice = 10,
                    DiscountedPrice = 5,
                    QuantityAvailable = 20,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                    Status = ProductStatus.Active
                });
            }

            if (bakeryCategory != null)
            {
                context.Products.Add(new Product
                {
                    StoreId = store.Id,
                    CategoryId = bakeryCategory.Id,
                    Title = "Fresh Whole Wheat Toast",
                    TitleAr = "توست بني طازج",
                    Description = "Soft bakery toast slices",
                    OriginalPrice = 15,
                    DiscountedPrice = 8,
                    QuantityAvailable = 10,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                    Status = ProductStatus.Active
                });
            }

            if (dairyCategory != null)
            {
                context.Products.Add(new Product
                {
                    StoreId = store.Id,
                    CategoryId = dairyCategory.Id,
                    Title = "Greek Yogurt",
                    TitleAr = "زبادي يوناني",
                    Description = "Thick high-protein yogurt",
                    OriginalPrice = 25,
                    DiscountedPrice = 15,
                    QuantityAvailable = 30,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                    Status = ProductStatus.Active
                });
            }

            await context.SaveChangesAsync();

            // Seed pending products with low AI confidence for moderation testing
            if (fruitsCategory != null)
            {
                var pendingApple = new Product
                {
                    StoreId = store.Id,
                    CategoryId = fruitsCategory.Id,
                    Title = "Unreviewed Red Apples",
                    TitleAr = "تفاح أحمر غير مراجع",
                    Description = "Imported apples requiring moderation review",
                    OriginalPrice = 30,
                    DiscountedPrice = 15,
                    QuantityAvailable = 50,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                    Status = ProductStatus.PendingModeration
                };
                context.Products.Add(pendingApple);
                await context.SaveChangesAsync();

                context.AIRecognitionResults.Add(new AIRecognitionResult
                {
                    ProductId = pendingApple.Id,
                    DetectedProduct = "Apples",
                    ConfidenceScore = 0.52,
                    Reviewed = false
                });
            }

            if (bakeryCategory != null)
            {
                var pendingCroissant = new Product
                {
                    StoreId = store.Id,
                    CategoryId = bakeryCategory.Id,
                    Title = "Unreviewed Butter Croissant",
                    TitleAr = "كرواسون زبدة غير مراجع",
                    Description = "Fresh croissant needing status verification",
                    OriginalPrice = 12,
                    DiscountedPrice = 6,
                    QuantityAvailable = 15,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                    Status = ProductStatus.PendingModeration
                };
                context.Products.Add(pendingCroissant);
                await context.SaveChangesAsync();

                context.AIRecognitionResults.Add(new AIRecognitionResult
                {
                    ProductId = pendingCroissant.Id,
                    DetectedProduct = "Pastry",
                    ConfidenceScore = 0.65,
                    Reviewed = false
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded Spinneys merchant, products, and pending low AI confidence products.");
        }

        // Seed Carrefour Merchant
        var carrefourEmail = "merchant.carrefour@example.com";
        var carrefourUser = await userManager.FindByEmailAsync(carrefourEmail);
        if (carrefourUser == null)
        {
            carrefourUser = new ApplicationUser
            {
                UserName = carrefourEmail,
                Email = carrefourEmail,
                EmailConfirmed = true,
                FullName = "Carrefour Merchant",
                Status = UserStatus.Active
            };
            await userManager.CreateAsync(carrefourUser, "Password@123");
            await userManager.AddToRoleAsync(carrefourUser, AppRole.Merchant);

            var store = new Store
            {
                Id = Guid.NewGuid(),
                OwnerId = carrefourUser.Id,
                Name = "Carrefour Market",
                VerificationStatus = VerificationStatus.Verified,
                Latitude = 30.0700,
                Longitude = 31.3300,
                City = "Cairo",
                Neighborhood = "Heliopolis",
                Street = "El Merghany St",
                BuildingNo = "100",
                AverageRating = 4.2
            };
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            var fruitsCategory = context.Categories.FirstOrDefault(c => c.Name == "Fruits & Vegetables");
            var bakeryCategory = context.Categories.FirstOrDefault(c => c.Name == "Bakery");
            var dairyCategory = context.Categories.FirstOrDefault(c => c.Name == "Dairy & Eggs");
            var mealsCategory = context.Categories.FirstOrDefault(c => c.Name == "Meals");

            if (fruitsCategory != null)
            {
                context.Products.Add(new Product
                {
                    StoreId = store.Id,
                    CategoryId = fruitsCategory.Id,
                    Title = "Red Apple Bag",
                    TitleAr = "كيس تفاح أحمر",
                    OriginalPrice = 40,
                    DiscountedPrice = 20,
                    QuantityAvailable = 15,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)),
                    Status = ProductStatus.Active
                });
            }

            if (bakeryCategory != null)
            {
                context.Products.Add(new Product
                {
                    StoreId = store.Id,
                    CategoryId = bakeryCategory.Id,
                    Title = "Croissant Box",
                    TitleAr = "علبة كرواسون",
                    OriginalPrice = 30,
                    DiscountedPrice = 18,
                    QuantityAvailable = 12,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                    Status = ProductStatus.Active
                });
            }

            if (dairyCategory != null)
            {
                context.Products.Add(new Product
                {
                    StoreId = store.Id,
                    CategoryId = dairyCategory.Id,
                    Title = "Cheddar Cheese Block",
                    TitleAr = "قالب جبن شيدر",
                    OriginalPrice = 80,
                    DiscountedPrice = 45,
                    QuantityAvailable = 8,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
                    Status = ProductStatus.Active
                });
            }

            if (mealsCategory != null)
            {
                context.Products.Add(new Product
                {
                    StoreId = store.Id,
                    CategoryId = mealsCategory.Id,
                    Title = "Beef Lasagna Ready Meal",
                    TitleAr = "وجبة لازانيا لحم جاهزة",
                    OriginalPrice = 120,
                    DiscountedPrice = 60,
                    QuantityAvailable = 5,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                    Status = ProductStatus.Active
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded Carrefour merchant and products.");
        }

        // Seed a pending store
        var pendingEmail = "merchant.pending@example.com";
        var pendingUser = await userManager.FindByEmailAsync(pendingEmail);
        if (pendingUser == null)
        {
            pendingUser = new ApplicationUser
            {
                UserName = pendingEmail,
                Email = pendingEmail,
                EmailConfirmed = true,
                FullName = "Pending Corner Shop Owner",
                Status = UserStatus.Active
            };
            await userManager.CreateAsync(pendingUser, "Password@123");
            await userManager.AddToRoleAsync(pendingUser, AppRole.Merchant);

            var store = new Store
            {
                Id = Guid.NewGuid(),
                OwnerId = pendingUser.Id,
                Name = "Pending Corner Shop",
                VerificationStatus = VerificationStatus.Pending,
                Latitude = 30.0100,
                Longitude = 31.2100,
                City = "Cairo",
                Neighborhood = "Zamalek",
                Street = "26th July St",
                BuildingNo = "12",
                AverageRating = 0.0
            };
            context.Stores.Add(store);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded pending merchant user & store.");
        }

        // Ensure every single product in the database has an AIRecognitionResult
        var allProducts = await context.Products
            .Include(p => p.AIRecognitionResult)
            .ToListAsync();

        var random = new Random();
        foreach (var p in allProducts)
        {
            if (p.AIRecognitionResult == null)
            {
                var score = 0.75 + (random.NextDouble() * 0.23); // Generate score between 0.75 and 0.98
                context.AIRecognitionResults.Add(new AIRecognitionResult
                {
                    ProductId = p.Id,
                    DetectedProduct = p.Title,
                    ConfidenceScore = Math.Round(score, 2),
                    Reviewed = p.Status == ProductStatus.Active
                });
            }
        }
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded AIRecognitionResult for all products in database.");
    }
}