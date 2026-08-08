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

using System.IO;
using System.Text.Json;

namespace FoodLoop.Infrastructure.Identity;

/// <summary>
/// Ensures the RBAC roles (Customer, Merchant, Charity, Admin)
/// and the default administrator account exist on startup.
/// </summary>
public static class IdentitySeeder
{
    private static Guid GetGuidForString(string id)
    {
        if (Guid.TryParse(id, out var g)) return g;
        var normalized = id.ToLowerInvariant().Replace("-", "").Replace("0", "");
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalized));
            return new Guid(hash);
        }
    }

    private static string? GetJsonString(JsonElement element, string propName)
    {
        return element.TryGetProperty(propName, out var prop) && prop.ValueKind != JsonValueKind.Null ? prop.GetString() : null;
    }

    private static string? GetJsonStringOrNumber(JsonElement element, string propName)
    {
        if (element.TryGetProperty(propName, out var prop) && prop.ValueKind != JsonValueKind.Null)
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
            return prop.GetString();
        }
        return null;
    }

    private static double? GetJsonDouble(JsonElement element, string propName)
    {
        return element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.Number ? prop.GetDouble() : null;
    }

    private static int? GetJsonInt(JsonElement element, string propName)
    {
        return element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.Number ? prop.GetInt32() : null;
    }

    private static decimal? GetJsonDecimal(JsonElement element, string propName)
    {
        return element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.Number ? prop.GetDecimal() : null;
    }

    private static bool GetJsonBool(JsonElement element, string propName, bool defaultValue = false)
    {
        return element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.True;
    }

    private static T ParseEnum<T>(JsonElement element, string propName, T defaultValue) where T : struct
    {
        if (element.TryGetProperty(propName, out var prop) && prop.ValueKind != JsonValueKind.Null)
        {
            if (prop.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(T), prop.GetInt32()))
            {
                return (T)(object)prop.GetInt32();
            }
            else if (prop.ValueKind == JsonValueKind.String && Enum.TryParse<T>(prop.GetString(), true, out var result))
            {
                return result;
            }
        }
        return defaultValue;
    }

    private static string? FindSeedDataPath()
    {
        var searchPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "seeddata.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "seeddata.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "seeddata.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seeddata.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "seeddata.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "seeddata.json")
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }
        return null;
    }

    private static async Task<bool> SeedFromFileAsync(IServiceProvider services)
    {
        var seedPath = FindSeedDataPath();
        if (seedPath == null) return false;

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
        logger.LogInformation("Found seeddata.json at {SeedPath}. Seeding database...", seedPath);

        var jsonText = await File.ReadAllTextAsync(seedPath);
        using (var doc = JsonDocument.Parse(jsonText))
        {
            var root = doc.RootElement;
            var context = services.GetRequiredService<ApplicationDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            var roleIdMap = new Dictionary<Guid, Guid>();
            var userIdMap = new Dictionary<Guid, Guid>();
            var orgIdMap = new Dictionary<Guid, Guid>();
            var catIdMap = new Dictionary<Guid, Guid>();
            var prodIdMap = new Dictionary<Guid, Guid>();
            var orderIdMap = new Dictionary<Guid, Guid>();
            var ticketIdMap = new Dictionary<Guid, Guid>();

            // Seed Roles
            if (root.TryGetProperty("Roles", out var rolesProp) && rolesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in rolesProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    var roleName = element.GetProperty("Name").GetString()!;
                    var existingRole = await roleManager.FindByNameAsync(roleName);
                    if (existingRole != null)
                    {
                        roleIdMap[id] = existingRole.Id;
                    }
                    else
                    {
                        var role = new ApplicationRole(roleName)
                        {
                            Id = id,
                            NormalizedName = element.GetProperty("NormalizedName").GetString()?.ToUpperInvariant(),
                            ConcurrencyStamp = GetJsonString(element, "ConcurrencyStamp")
                        };
                        await roleManager.CreateAsync(role);
                        roleIdMap[id] = id;
                    }
                }
            }

            // Seed Users
            if (root.TryGetProperty("Users", out var usersProp) && usersProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in usersProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    var email = element.GetProperty("Email").GetString()!;
                    var existingUser = await userManager.FindByEmailAsync(email);
                    if (existingUser != null)
                    {
                        userIdMap[id] = existingUser.Id;
                    }
                    else
                    {
                        existingUser = await userManager.FindByIdAsync(id.ToString());
                        if (existingUser != null)
                        {
                            userIdMap[id] = existingUser.Id;
                        }
                        else
                        {
                            var user = new ApplicationUser
                            {
                                Id = id,
                                UserName = element.GetProperty("UserName").GetString(),
                                NormalizedUserName = element.GetProperty("NormalizedUserName").GetString()?.ToUpperInvariant(),
                                Email = email,
                                NormalizedEmail = element.GetProperty("NormalizedEmail").GetString()?.ToUpperInvariant(),
                                EmailConfirmed = GetJsonBool(element, "EmailConfirmed"),
                                PasswordHash = GetJsonString(element, "PasswordHash"),
                                SecurityStamp = GetJsonString(element, "SecurityStamp") ?? Guid.NewGuid().ToString(),
                                ConcurrencyStamp = GetJsonString(element, "ConcurrencyStamp") ?? Guid.NewGuid().ToString(),
                                PhoneNumber = GetJsonString(element, "PhoneNumber"),
                                PhoneNumberConfirmed = GetJsonBool(element, "PhoneNumberConfirmed"),
                                TwoFactorEnabled = GetJsonBool(element, "TwoFactorEnabled"),
                                LockoutEnabled = GetJsonBool(element, "LockoutEnabled"),
                                AccessFailedCount = GetJsonInt(element, "AccessFailedCount") ?? 0,
                                FullName = GetJsonString(element, "FullName") ?? "",
                                Language = GetJsonString(element, "Language") ?? "en",
                                Status = ParseEnum(element, "Status", UserStatus.Active),
                                OrderUpdatesEnabled = GetJsonBool(element, "OrderUpdatesEnabled"),
                                MarketingNotificationsEnabled = GetJsonBool(element, "MarketingNotificationsEnabled"),
                                CreatedAt = element.TryGetProperty("CreatedAt", out var caProp) ? DateTimeOffset.Parse(caProp.GetString()!) : DateTimeOffset.UtcNow
                            };

                            await userManager.CreateAsync(user);
                            userIdMap[id] = id;
                        }
                    }
                }
            }

            // Seed UserRoles
            if (root.TryGetProperty("UserRoles", out var userRolesProp) && userRolesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in userRolesProp.EnumerateArray())
                {
                    var userIdInput = GetGuidForString(element.GetProperty("UserId").GetString()!);
                    var roleIdInput = GetGuidForString(element.GetProperty("RoleId").GetString()!);
                    var userId = userIdMap.TryGetValue(userIdInput, out var mappedUserId) ? mappedUserId : userIdInput;
                    var roleId = roleIdMap.TryGetValue(roleIdInput, out var mappedRoleId) ? mappedRoleId : roleIdInput;

                    if (!context.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == roleId))
                    {
                        context.UserRoles.Add(new IdentityUserRole<Guid>
                        {
                            UserId = userId,
                            RoleId = roleId
                        });
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed Addresses
            if (root.TryGetProperty("Addresses", out var addressesProp) && addressesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in addressesProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    if (!context.Addresses.Any(x => x.Id == id))
                    {
                        var userIdInput = GetGuidForString(element.GetProperty("UserId").GetString()!);
                        var userId = userIdMap.TryGetValue(userIdInput, out var mappedUserId) ? mappedUserId : userIdInput;

                        var address = new Address
                        {
                            Id = id,
                            UserId = userId,
                            AddressType = ParseEnum(element, "Label", AddressType.Home),
                            City = GetJsonString(element, "City") ?? "",
                            District = GetJsonString(element, "District") ?? "",
                            Street = GetJsonString(element, "Street") ?? "",
                            BuildingNo = GetJsonStringOrNumber(element, "BuildingNo"),
                            Floor = GetJsonStringOrNumber(element, "Floor"),
                            ApartmentNo = GetJsonStringOrNumber(element, "ApartmentNo"),
                            Notes = GetJsonString(element, "Notes"),
                            Latitude = GetJsonDouble(element, "Latitude") ?? 0,
                            Longitude = GetJsonDouble(element, "Longitude") ?? 0,
                            IsDefault = GetJsonBool(element, "IsDefault"),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Addresses.Add(address);
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed Organizations
            if (root.TryGetProperty("Organizations", out var orgsProp) && orgsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in orgsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    var ownerIdInput = GetGuidForString(element.GetProperty("OwnerId").GetString()!);
                    var ownerId = userIdMap.TryGetValue(ownerIdInput, out var mappedOwnerId) ? mappedOwnerId : ownerIdInput;
                    var name = GetJsonString(element, "Name") ?? "";

                    var existingOrg = context.Organizations.FirstOrDefault(o => o.OwnerId == ownerId || o.Name == name || o.Id == id);
                    if (existingOrg != null)
                    {
                        orgIdMap[id] = existingOrg.Id;
                        if (existingOrg.VerificationStatus != VerificationStatus.Verified)
                        {
                            existingOrg.VerificationStatus = VerificationStatus.Verified;
                            context.Organizations.Update(existingOrg);
                            await context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        var org = new Organization
                        {
                            Id = id,
                            OwnerId = ownerId,
                            Name = name,
                            NameAr = GetJsonString(element, "NameAr"),
                            Description = GetJsonString(element, "Description"),
                            DescriptionAr = GetJsonString(element, "DescriptionAr"),
                            Logo = GetJsonString(element, "Logo"),
                            Phone = GetJsonString(element, "Phone"),
                            Email = GetJsonString(element, "Email"),
                            BusinessCategory = element.TryGetProperty("BusinessCategory", out var bcProp) ? ParseEnum<BusinessCategory>(element, "BusinessCategory", BusinessCategory.Supermarket) : (BusinessCategory?)null,
                            Governorate = GetJsonString(element, "Governorate"),
                            City = GetJsonString(element, "City"),
                            Neighborhood = GetJsonString(element, "Neighborhood") ?? GetJsonString(element, "District"),
                            Street = GetJsonString(element, "Street"),
                            BuildingNo = GetJsonStringOrNumber(element, "BuildingNo"),
                            Latitude = GetJsonDouble(element, "Latitude"),
                            Longitude = GetJsonDouble(element, "Longitude"),
                            OpeningHours = GetJsonString(element, "OpeningHours"),
                            VerificationStatus = VerificationStatus.Verified,
                            AdminNote = GetJsonString(element, "AdminNote") ?? GetJsonString(element, "AdminNotes"),
                            AverageRating = GetJsonDouble(element, "AverageRating") ?? 0,
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Organizations.Add(org);
                        await context.SaveChangesAsync();
                        orgIdMap[id] = id;
                    }
                }
            }

            // Seed OrganizationVerifications
            if (root.TryGetProperty("StoreVerifications", out var verProp) && verProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in verProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    if (!context.OrganizationVerifications.Any(x => x.Id == id))
                    {
                        var orgIdInput = GetGuidForString(element.GetProperty("OrganizationId").GetString()!);
                        var orgId = orgIdMap.TryGetValue(orgIdInput, out var mappedOrgId) ? mappedOrgId : orgIdInput;

                        var reviewedByInput = element.TryGetProperty("ReviewedBy", out var rb) && rb.ValueKind != JsonValueKind.Null ? GetGuidForString(rb.GetString()!) : (Guid?)null;
                        var reviewedBy = reviewedByInput.HasValue && userIdMap.TryGetValue(reviewedByInput.Value, out var mappedRb) ? mappedRb : reviewedByInput;

                        var ver = new OrganizationVerification
                        {
                            Id = id,
                            OrganizationId = orgId,
                            VerificationType = ParseEnum(element, "VerificationType", UploadDocumentType.CommercialRegistration),
                            DocumentUrl = GetJsonString(element, "FileUrl") ?? GetJsonString(element, "DocumentUrl") ?? "",
                            Status = ParseEnum(element, "Status", VerificationStatus.Pending),
                            ReviewNote = GetJsonString(element, "AdminNotes") ?? GetJsonString(element, "ReviewNote"),
                            ReviewedBy = reviewedBy,
                            ReviewedAt = element.TryGetProperty("ReviewedAt", out var ra) && ra.ValueKind != JsonValueKind.Null ? DateTimeOffset.Parse(ra.GetString()!) : (DateTimeOffset?)null,
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.OrganizationVerifications.Add(ver);
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed Categories
            if (root.TryGetProperty("Categories", out var catsProp) && catsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in catsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    var name = GetJsonString(element, "Name") ?? "";
                    var existingCat = context.Categories.FirstOrDefault(c => c.Name == name || c.Id == id);
                    if (existingCat != null)
                    {
                        catIdMap[id] = existingCat.Id;
                    }
                    else
                    {
                        var cat = new Category
                        {
                            Id = id,
                            Name = name,
                            NameAr = GetJsonString(element, "NameAr"),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Categories.Add(cat);
                        await context.SaveChangesAsync();
                        catIdMap[id] = id;
                    }
                }
            }

            // Seed Products
            if (root.TryGetProperty("Products", out var prdsProp) && prdsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in prdsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    var title = GetJsonString(element, "Title") ?? "";
                    var orgIdInput = GetGuidForString(element.GetProperty("OrganizationId").GetString()!);
                    var orgId = orgIdMap.TryGetValue(orgIdInput, out var mappedOrgId) ? mappedOrgId : orgIdInput;

                    var existingProd = context.Products.FirstOrDefault(p => (p.Title == title && p.OrganizationId == orgId) || p.Id == id);
                    if (existingProd != null)
                    {
                        prodIdMap[id] = existingProd.Id;
                    }
                    else
                    {
                        var catIdInput = GetGuidForString(element.GetProperty("CategoryId").GetString()!);
                        var catId = catIdMap.TryGetValue(catIdInput, out var mappedCatId) ? mappedCatId : catIdInput;

                        var prd = new Product
                        {
                            Id = id,
                            OrganizationId = orgId,
                            CategoryId = catId,
                            Title = title,
                            Description = GetJsonString(element, "Description"),
                            OriginalPrice = GetJsonDecimal(element, "OriginalPrice") ?? 0,
                            DiscountedPrice = GetJsonDecimal(element, "DiscountedPrice") ?? 0,
                            QuantityAvailable = GetJsonInt(element, "QuantityAvailable") ?? 0,
                            ExpirationDate = element.TryGetProperty("ExpirationDate", out var ed) ? DateOnly.Parse(ed.GetString()!) : DateOnly.FromDateTime(DateTime.Today),
                            Status = ParseEnum(element, "Status", ProductStatus.Active),
                            ModerationNote = GetJsonString(element, "ModerationNote"),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Products.Add(prd);
                        await context.SaveChangesAsync();
                        prodIdMap[id] = id;
                    }
                }
            }

            // Seed ProductImages
            if (root.TryGetProperty("ProductImages", out var imgsProp) && imgsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in imgsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    if (!context.ProductImages.Any(x => x.Id == id))
                    {
                        var prodIdInput = GetGuidForString(element.GetProperty("ProductId").GetString()!);
                        var prodId = prodIdMap.TryGetValue(prodIdInput, out var mappedProdId) ? mappedProdId : prodIdInput;

                        var img = new ProductImage
                        {
                            Id = id,
                            ProductId = prodId,
                            ImageUrl = GetJsonString(element, "ImageUrl") ?? "",
                            DisplayOrder = GetJsonInt(element, "DisplayOrder") ?? 0,
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.ProductImages.Add(img);
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed Orders
            if (root.TryGetProperty("Orders", out var ordsProp) && ordsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in ordsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    var existingOrder = context.Orders.FirstOrDefault(o => o.Id == id);
                    if (existingOrder != null)
                    {
                        orderIdMap[id] = existingOrder.Id;
                    }
                    else
                    {
                        var userIdInput = GetGuidForString(element.GetProperty("UserId").GetString()!);
                        var userId = userIdMap.TryGetValue(userIdInput, out var mappedUserId) ? mappedUserId : userIdInput;

                        var ord = new Order
                        {
                            Id = id,
                            UserId = userId,
                            TotalAmount = GetJsonDecimal(element, "TotalAmount") ?? 0,
                            PaymentStatus = ParseEnum(element, "PaymentStatus", PaymentStatus.Pending),
                            OrderStatus = ParseEnum(element, "OrderStatus", OrderStatus.Pending),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Orders.Add(ord);
                        await context.SaveChangesAsync();
                        orderIdMap[id] = id;
                    }
                }
            }

            // Seed OrderItems
            if (root.TryGetProperty("OrderItems", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in itemsProp.EnumerateArray())
                {
                    var orderIdInput = GetGuidForString(element.GetProperty("OrderId").GetString()!);
                    var productIdInput = GetGuidForString(element.GetProperty("ProductId").GetString()!);
                    var orderId = orderIdMap.TryGetValue(orderIdInput, out var mappedOrderId) ? mappedOrderId : orderIdInput;
                    var productId = prodIdMap.TryGetValue(productIdInput, out var mappedProdId) ? mappedProdId : productIdInput;

                    if (!context.OrderItems.Any(oi => oi.OrderId == orderId && oi.ProductId == productId))
                    {
                        context.OrderItems.Add(new OrderItem
                        {
                            OrderId = orderId,
                            ProductId = productId,
                            Quantity = GetJsonInt(element, "Quantity") ?? 0,
                            UnitPrice = GetJsonDecimal(element, "UnitPrice") ?? 0
                        });
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed Payments
            if (root.TryGetProperty("Payments", out var paysProp) && paysProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in paysProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    if (!context.Payments.Any(x => x.Id == id))
                    {
                        var orderIdInput = GetGuidForString(element.GetProperty("OrderId").GetString()!);
                        var orderId = orderIdMap.TryGetValue(orderIdInput, out var mappedOrderId) ? mappedOrderId : orderIdInput;

                        var pay = new Payment
                        {
                            Id = id,
                            OrderId = orderId,
                            Method = GetJsonString(element, "Method") ?? "",
                            Amount = GetJsonDecimal(element, "Amount") ?? 0,
                            Status = ParseEnum(element, "Status", PaymentStatus.Pending),
                            TransactionReference = GetJsonString(element, "TransactionReference"),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Payments.Add(pay);
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed Reviews
            if (root.TryGetProperty("Reviews", out var revsProp) && revsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in revsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    if (!context.Reviews.Any(x => x.Id == id))
                    {
                        var orderIdInput = GetGuidForString(element.GetProperty("OrderId").GetString()!);
                        var orderId = orderIdMap.TryGetValue(orderIdInput, out var mappedOrderId) ? mappedOrderId : orderIdInput;

                        var userIdInput = GetGuidForString(element.GetProperty("UserId").GetString()!);
                        var userId = userIdMap.TryGetValue(userIdInput, out var mappedUserId) ? mappedUserId : userIdInput;

                        var orgIdInput = GetGuidForString(element.GetProperty("OrganizationId").GetString()!);
                        var orgId = orgIdMap.TryGetValue(orgIdInput, out var mappedOrgId) ? mappedOrgId : orgIdInput;

                        var rev = new Review
                        {
                            Id = id,
                            OrderId = orderId,
                            UserId = userId,
                            OrganizationId = orgId,
                            Rating = GetJsonInt(element, "Rating") ?? 5,
                            Comment = GetJsonString(element, "Comment"),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Reviews.Add(rev);
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed Favorites
            if (root.TryGetProperty("Favorites", out var favsProp) && favsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in favsProp.EnumerateArray())
                {
                    var userIdInput = GetGuidForString(element.GetProperty("UserId").GetString()!);
                    var productIdInput = GetGuidForString(element.GetProperty("ProductId").GetString()!);
                    var userId = userIdMap.TryGetValue(userIdInput, out var mappedUserId) ? mappedUserId : userIdInput;
                    var productId = prodIdMap.TryGetValue(productIdInput, out var mappedProdId) ? mappedProdId : productIdInput;

                    if (!context.Favorites.Any(x => x.UserId == userId && x.ProductId == productId))
                    {
                        var fav = new Favorite
                        {
                            UserId = userId,
                            ProductId = productId,
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Favorites.Add(fav);
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed Notifications
            if (root.TryGetProperty("Notifications", out var notifsProp) && notifsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in notifsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    if (!context.Notifications.Any(x => x.Id == id))
                    {
                        var userIdInput = GetGuidForString(element.GetProperty("UserId").GetString()!);
                        var userId = userIdMap.TryGetValue(userIdInput, out var mappedUserId) ? mappedUserId : userIdInput;

                        var notif = new Notification
                        {
                            Id = id,
                            UserId = userId,
                            Title = GetJsonString(element, "Title") ?? "",
                            Body = GetJsonString(element, "Body") ?? "",
                            Type = GetJsonString(element, "Type") ?? "",
                            IsRead = GetJsonBool(element, "IsRead"),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.Notifications.Add(notif);
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed SupportTickets
            if (root.TryGetProperty("SupportTickets", out var tksProp) && tksProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in tksProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    var existingTicket = context.SupportTickets.FirstOrDefault(t => t.Id == id);
                    if (existingTicket != null)
                    {
                        ticketIdMap[id] = existingTicket.Id;
                    }
                    else
                    {
                        var userIdInput = GetGuidForString(element.GetProperty("UserId").GetString()!);
                        var userId = userIdMap.TryGetValue(userIdInput, out var mappedUserId) ? mappedUserId : userIdInput;

                        var tk = new SupportTicket
                        {
                            Id = id,
                            UserId = userId,
                            Category = GetJsonString(element, "Category") ?? "",
                            Priority = ParseEnum(element, "Priority", TicketPriority.Normal),
                            Status = ParseEnum(element, "Status", TicketStatus.Open),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.SupportTickets.Add(tk);
                        await context.SaveChangesAsync();
                        ticketIdMap[id] = id;
                    }
                }
            }

            // Seed TicketMessages
            if (root.TryGetProperty("TicketMessages", out var msgsProp) && msgsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in msgsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    if (!context.TicketMessages.Any(x => x.Id == id))
                    {
                        var ticketIdInput = GetGuidForString(element.GetProperty("SupportTicketId").GetString()!);
                        var ticketId = ticketIdMap.TryGetValue(ticketIdInput, out var mappedTicketId) ? mappedTicketId : ticketIdInput;

                        var senderIdInput = GetGuidForString(element.GetProperty("SenderId").GetString()!);
                        var senderId = userIdMap.TryGetValue(senderIdInput, out var mappedSenderId) ? mappedSenderId : senderIdInput;

                        var msg = new TicketMessage
                        {
                            Id = id,
                            TicketId = ticketId,
                            SenderId = senderId,
                            Message = GetJsonString(element, "Message") ?? "",
                            Attachment = GetJsonString(element, "Attachment"),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.TicketMessages.Add(msg);
                    }
                }
                await context.SaveChangesAsync();
            }

            // Seed AuditLogs
            if (root.TryGetProperty("AuditLogs", out var logsProp) && logsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in logsProp.EnumerateArray())
                {
                    var id = GetGuidForString(element.GetProperty("Id").GetString()!);
                    if (!context.AuditLogs.Any(x => x.Id == id))
                    {
                        var userIdInput = element.TryGetProperty("UserId", out var uid) && uid.ValueKind != JsonValueKind.Null ? GetGuidForString(uid.GetString()!) : (Guid?)null;
                        var userId = userIdInput.HasValue && userIdMap.TryGetValue(userIdInput.Value, out var mappedUid) ? mappedUid : userIdInput;

                        var orgIdInput = element.TryGetProperty("OrganizationId", out var oid) && oid.ValueKind != JsonValueKind.Null ? GetGuidForString(oid.GetString()!) : (Guid?)null;
                        var orgId = orgIdInput.HasValue && orgIdMap.TryGetValue(orgIdInput.Value, out var mappedOid) ? mappedOid : orgIdInput;

                        var log = new AuditLog
                        {
                            Id = id,
                            UserId = userId,
                            OrganizationId = orgId,
                            EventType = GetJsonString(element, "EventType") ?? "",
                            Title = GetJsonString(element, "Title") ?? "",
                            Description = GetJsonString(element, "Description") ?? "",
                            IpAddress = GetJsonString(element, "IpAddress"),
                            CreatedAt = element.TryGetProperty("CreatedAt", out var ca) ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.UtcNow
                        };
                        context.AuditLogs.Add(log);
                    }
                }
                await context.SaveChangesAsync();
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
                    var score = 0.75 + (random.NextDouble() * 0.23);
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
            logger.LogInformation("Finished seeding from seeddata.json.");
            return true;
        }
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        if (await SeedFromFileAsync(services))
        {
            return;
        }

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
            ("Fruits & Vegetables", "Ø®Ø¶Ø±ÙˆØ§Øª ÙˆÙÙˆØ§ÙƒÙ‡"),
            ("Bakery", "Ù…Ø®Ø¨ÙˆØ²Ø§Øª"),
            ("Dairy & Eggs", "Ø£Ù„Ø¨Ø§Ù† ÙˆØ¨ÙŠØ¶"),
            ("Meals", "ÙˆØ¬Ø¨Ø§Øª Ø¬Ø§Ù‡Ø²Ø©"),
            ("Groceries", "Ù…ÙˆØ§Ø¯ ØºØ°Ø§Ø¦ÙŠØ©"),
            ("Desserts", "Ø­Ù„ÙˆÙŠØ§Øª")
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

            var organization = new Organization
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
            context.Organizations.Add(organization);
            await context.SaveChangesAsync();

            var fruitsCategory = context.Categories.FirstOrDefault(c => c.Name == "Fruits & Vegetables");
            var bakeryCategory = context.Categories.FirstOrDefault(c => c.Name == "Bakery");
            var dairyCategory = context.Categories.FirstOrDefault(c => c.Name == "Dairy & Eggs");

            if (fruitsCategory != null)
            {
                context.Products.Add(new Product
                {
                    OrganizationId = organization.Id,
                    CategoryId = fruitsCategory.Id,
                    Title = "Organic Bananas",                    Description = "Sweet import organic bananas",
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
                    OrganizationId = organization.Id,
                    CategoryId = bakeryCategory.Id,
                    Title = "Fresh Whole Wheat Toast",                    Description = "Soft bakery toast slices",
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
                    OrganizationId = organization.Id,
                    CategoryId = dairyCategory.Id,
                    Title = "Greek Yogurt",                    Description = "Thick high-protein yogurt",
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
                    OrganizationId = organization.Id,
                    CategoryId = fruitsCategory.Id,
                    Title = "Unreviewed Red Apples",                    Description = "Imported apples requiring moderation review",
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
                    OrganizationId = organization.Id,
                    CategoryId = bakeryCategory.Id,
                    Title = "Unreviewed Butter Croissant",                    Description = "Fresh croissant needing status verification",
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

            var organization = new Organization
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
            context.Organizations.Add(organization);
            await context.SaveChangesAsync();

            var fruitsCategory = context.Categories.FirstOrDefault(c => c.Name == "Fruits & Vegetables");
            var bakeryCategory = context.Categories.FirstOrDefault(c => c.Name == "Bakery");
            var dairyCategory = context.Categories.FirstOrDefault(c => c.Name == "Dairy & Eggs");
            var mealsCategory = context.Categories.FirstOrDefault(c => c.Name == "Meals");

            if (fruitsCategory != null)
            {
                context.Products.Add(new Product
                {
                    OrganizationId = organization.Id,
                    CategoryId = fruitsCategory.Id,
                    Title = "Red Apple Bag",                    OriginalPrice = 40,
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
                    OrganizationId = organization.Id,
                    CategoryId = bakeryCategory.Id,
                    Title = "Croissant Box",                    OriginalPrice = 30,
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
                    OrganizationId = organization.Id,
                    CategoryId = dairyCategory.Id,
                    Title = "Cheddar Cheese Block",                    OriginalPrice = 80,
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
                    OrganizationId = organization.Id,
                    CategoryId = mealsCategory.Id,
                    Title = "Beef Lasagna Ready Meal",                    OriginalPrice = 120,
                    DiscountedPrice = 60,
                    QuantityAvailable = 5,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                    Status = ProductStatus.Active
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded Carrefour merchant and products.");
        }

        // Seed a pending organization
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

            var organization = new Organization
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
            context.Organizations.Add(organization);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded pending merchant user & organization.");
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
