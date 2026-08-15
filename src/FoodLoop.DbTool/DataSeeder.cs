using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FoodLoop.DbTool;

public static class DataSeeder
{
    public static async Task SeedLargeDatasetAsync(ApplicationDbContext db)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("    FOODLOOP DATABASE LARGE-SCALE DATA SEEDING        ");
        Console.WriteLine("=======================================================\n");
        Console.ResetColor();

        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var random = new Random(42);

        // -------------------------------------------------------------
        // 1. ROLES
        // -------------------------------------------------------------
        Console.WriteLine("--> [1/15] Seeding Security Roles...");
        var roles = new List<ApplicationRole>
        {
            new() { Id = Guid.NewGuid(), Name = AppRole.Admin, NormalizedName = AppRole.Admin.ToUpper(), ConcurrencyStamp = Guid.NewGuid().ToString() },
            new() { Id = Guid.NewGuid(), Name = AppRole.Merchant, NormalizedName = AppRole.Merchant.ToUpper(), ConcurrencyStamp = Guid.NewGuid().ToString() },
            new() { Id = Guid.NewGuid(), Name = AppRole.Customer, NormalizedName = AppRole.Customer.ToUpper(), ConcurrencyStamp = Guid.NewGuid().ToString() },
            new() { Id = Guid.NewGuid(), Name = AppRole.Charity, NormalizedName = AppRole.Charity.ToUpper(), ConcurrencyStamp = Guid.NewGuid().ToString() }
        };
        await db.Roles.AddRangeAsync(roles);
        await db.SaveChangesAsync();

        var adminRoleId = roles.First(r => r.Name == AppRole.Admin).Id;
        var merchantRoleId = roles.First(r => r.Name == AppRole.Merchant).Id;
        var customerRoleId = roles.First(r => r.Name == AppRole.Customer).Id;
        var charityRoleId = roles.First(r => r.Name == AppRole.Charity).Id;

        // -------------------------------------------------------------
        // 2. USERS & USER ROLES
        // -------------------------------------------------------------
        Console.WriteLine("--> [2/15] Seeding Users (Admin, Merchants, Charities, Customers)...");
        var users = new List<ApplicationUser>();
        var userRoles = new List<IdentityUserRole<Guid>>();

        // 2.1 System Admin
        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "admin@foodloop.com",
            NormalizedUserName = "ADMIN@FOODLOOP.COM",
            Email = "admin@foodloop.com",
            NormalizedEmail = "ADMIN@FOODLOOP.COM",
            EmailConfirmed = true,
            FullName = "System Administrator",
            PhoneNumber = "+201011111111",
            PhoneNumberConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Status = UserStatus.Active,
            Language = "en",
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-6)
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin@123");
        users.Add(adminUser);
        userRoles.Add(new IdentityUserRole<Guid> { UserId = adminUser.Id, RoleId = adminRoleId });

        // 2.2 Merchants (10 stores)
        var merchantInfos = new[]
        {
            ("merchant.spinneys@example.com", "Spinneys Egypt Manager", "+201020000001", "Spinneys Supermarket", BusinessCategory.Supermarket, "Zamalek", "26th of July St", 30.0626, 31.2223),
            ("merchant.carrefour@example.com", "Carrefour Store Lead", "+201020000002", "Carrefour Hypermarket", BusinessCategory.Supermarket, "Maadi", "Ring Road, City Centre", 29.9737, 31.3015),
            ("merchant.seoudi@example.com", "Seoudi Operations Head", "+201020000003", "Seoudi Supermarket", BusinessCategory.Supermarket, "Dokki", "Mesaha Square", 30.0382, 31.2119),
            ("merchant.metro@example.com", "Metro Market Officer", "+201020000004", "Metro Market", BusinessCategory.Supermarket, "Heliopolis", "Al Ahram St", 30.0901, 31.3285),
            ("merchant.gourmet@example.com", "Gourmet Fresh Lead", "+201020000005", "Gourmet Egypt", BusinessCategory.GroceryChain, "New Cairo", "Waterway Mall", 30.0312, 31.4721),
            ("merchant.tbs@example.com", "TBS Bakery Artisan", "+201020000006", "The Bakery Shop (TBS)", BusinessCategory.Bakery, "Zamalek", "Brazil St", 30.0594, 31.2187),
            ("merchant.freshfood@example.com", "Fresh Food Market Mgr", "+201020000007", "Fresh Food Market", BusinessCategory.Supermarket, "Sheikh Zayed", "Plaza 34", 30.0210, 30.9821),
            ("merchant.alfa@example.com", "Alfa Market Supervisor", "+201020000008", "Alfa Market", BusinessCategory.Supermarket, "Mohandessin", "Shehab St", 30.0542, 31.2001),
            ("merchant.kazyon@example.com", "Kazyon Store Branch Lead", "+201020000009", "Kazyon Market", BusinessCategory.ConvenienceStore, "Nasr City", "Abbas El Akkad", 30.0571, 31.3411),
            ("merchant.hyperone@example.com", "Hyper One Sales Lead", "+201020000010", "Hyper One Zayed", BusinessCategory.Supermarket, "Sheikh Zayed", "Desert Road Entrance", 30.0415, 30.9950)
        };

        var merchantUsers = new List<ApplicationUser>();
        foreach (var m in merchantInfos)
        {
            var u = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = m.Item1,
                NormalizedUserName = m.Item1.ToUpper(),
                Email = m.Item1,
                NormalizedEmail = m.Item1.ToUpper(),
                EmailConfirmed = true,
                FullName = m.Item2,
                PhoneNumber = m.Item3,
                PhoneNumberConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                Status = UserStatus.Active,
                Language = "en",
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-4)
            };
            u.PasswordHash = passwordHasher.HashPassword(u, "Password@123");
            users.Add(u);
            merchantUsers.Add(u);
            userRoles.Add(new IdentityUserRole<Guid> { UserId = u.Id, RoleId = merchantRoleId });
        }

        // 2.3 Charities (5 NGOs)
        var charityInfos = new[]
        {
            ("charity.foodbank@example.com", "Egyptian Food Bank Lead", "+201030000001", "Egyptian Food Bank (بنك الطعام)", "Tagamoa 3, New Cairo", 30.0121, 31.4231),
            ("charity.resala@example.com", "Resala NGO Director", "+201030000002", "Resala Charity Association", "Faisal, Giza", 30.0091, 31.1891),
            ("charity.orman@example.com", "Orman Association Rep", "+201030000003", "Orman Charity Association", "Haram, Giza", 29.9981, 31.1541),
            ("charity.misrelkheir@example.com", "Misr El Kheir Officer", "+201030000004", "Misr El Kheir Foundation", "Mokattam, Cairo", 30.0181, 31.3021),
            ("charity.baheya@example.com", "Baheya Community Rep", "+201030000005", "Baheya Foundation Food Support", "Haram, Giza", 30.0011, 31.1711)
        };

        var charityUsers = new List<ApplicationUser>();
        foreach (var c in charityInfos)
        {
            var u = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = c.Item1,
                NormalizedUserName = c.Item1.ToUpper(),
                Email = c.Item1,
                NormalizedEmail = c.Item1.ToUpper(),
                EmailConfirmed = true,
                FullName = c.Item2,
                PhoneNumber = c.Item3,
                PhoneNumberConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                Status = UserStatus.Active,
                Language = "ar",
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-5)
            };
            u.PasswordHash = passwordHasher.HashPassword(u, "Password@123");
            users.Add(u);
            charityUsers.Add(u);
            userRoles.Add(new IdentityUserRole<Guid> { UserId = u.Id, RoleId = charityRoleId });
        }

        // 2.4 Customers (25 active customers)
        var customerNames = new[]
        {
            ("Ahmed Hassan", "ahmed.hassan@example.com", "+201040000001", "Cairo", "Zamalek"),
            ("Sara Mahmoud", "sara.mahmoud@example.com", "+201040000002", "Giza", "Dokki"),
            ("Mohamed Aly", "mohamed.aly@example.com", "+201040000003", "Cairo", "Maadi"),
            ("Nour El-Din", "nour.eldin@example.com", "+201040000004", "Cairo", "Heliopolis"),
            ("Yasmine Tarek", "yasmine.tarek@example.com", "+201040000005", "Cairo", "New Cairo"),
            ("Omar Khaled", "omar.khaled@example.com", "+201040000006", "Giza", "Sheikh Zayed"),
            ("Mariam Ibrahim", "mariam.ibrahim@example.com", "+201040000007", "Giza", "Mohandessin"),
            ("Karim Mostafa", "karim.mostafa@example.com", "+201040000008", "Cairo", "Nasr City"),
            ("Laila Sherif", "laila.sherif@example.com", "+201040000009", "Alexandria", "Smouha"),
            ("Hassan Farouk", "hassan.farouk@example.com", "+201040000010", "Alexandria", "Gleem"),
            ("Dina Samir", "dina.samir@example.com", "+201040000011", "Cairo", "Shubra"),
            ("Tarek Nabil", "tarek.nabil@example.com", "+201040000012", "Giza", "Agouza"),
            ("Mona Adel", "mona.adel@example.com", "+201040000013", "Cairo", "Rehab"),
            ("Amr Essam", "amr.essam@example.com", "+201040000014", "Cairo", "Madinaty"),
            ("Salma Wael", "salma.wael@example.com", "+201040000015", "Giza", "6th of October"),
            ("Khaled Yasser", "khaled.yasser@example.com", "+201040000016", "Cairo", "Abbassia"),
            ("Heba Gamal", "heba.gamal@example.com", "+201040000017", "Alexandria", "Roushdy"),
            ("Ziad Ashraf", "ziad.ashraf@example.com", "+201040000018", "Cairo", "Manial"),
            ("Rania Fouad", "rania.fouad@example.com", "+201040000019", "Giza", "Haram"),
            ("Sherif Hamdy", "sherif.hamdy@example.com", "+201040000020", "Cairo", "Garden City"),
            ("Fatma Zaki", "fatma.zaki@example.com", "+201040000021", "Cairo", "Katameya"),
            ("Mostafa Lotfy", "mostafa.lotfy@example.com", "+201040000022", "Giza", "Imbaba"),
            ("Aya Medhat", "aya.medhat@example.com", "+201040000023", "Alexandria", "San Stefano"),
            ("Youssef Nader", "youssef.nader@example.com", "+201040000024", "Cairo", "Sheraton"),
            ("Reem Fathy", "reem.fathy@example.com", "+201040000025", "Giza", "Hadayek El Ahram")
        };

        var customerUsers = new List<ApplicationUser>();
        foreach (var cust in customerNames)
        {
            var u = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = cust.Item2,
                NormalizedUserName = cust.Item2.ToUpper(),
                Email = cust.Item2,
                NormalizedEmail = cust.Item2.ToUpper(),
                EmailConfirmed = true,
                FullName = cust.Item1,
                PhoneNumber = cust.Item3,
                PhoneNumberConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                Status = UserStatus.Active,
                Language = random.Next(2) == 0 ? "ar" : "en",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(10, 150))
            };
            u.PasswordHash = passwordHasher.HashPassword(u, "Password@123");
            users.Add(u);
            customerUsers.Add(u);
            userRoles.Add(new IdentityUserRole<Guid> { UserId = u.Id, RoleId = customerRoleId });
        }

        await db.Users.AddRangeAsync(users);
        await db.UserRoles.AddRangeAsync(userRoles);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 3. CATEGORIES
        // -------------------------------------------------------------
        Console.WriteLine("--> [3/15] Seeding Product Categories...");
        var categories = new List<Category>
        {
            new() { Id = Guid.NewGuid(), Name = "Bakery", NameAr = "مخبوزات", Icon = "https://images.unsplash.com/photo-1509440159596-0249088772ff?w=100" },
            new() { Id = Guid.NewGuid(), Name = "Dairy & Eggs", NameAr = "ألبان وبيض", Icon = "https://images.unsplash.com/photo-1628088062854-d1870b4553da?w=100" },
            new() { Id = Guid.NewGuid(), Name = "Fruits & Vegetables", NameAr = "خضار وفواكه", Icon = "https://images.unsplash.com/photo-1610832958506-aa56368176cf?w=100" },
            new() { Id = Guid.NewGuid(), Name = "Meat & Poultry", NameAr = "لحوم ودواجن", Icon = "https://images.unsplash.com/photo-1607623814075-e51df1bdc82f?w=100" },
            new() { Id = Guid.NewGuid(), Name = "Prepared Meals", NameAr = "وجبات جاهزة", Icon = "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=100" },
            new() { Id = Guid.NewGuid(), Name = "Beverages", NameAr = "مشروبات", Icon = "https://images.unsplash.com/photo-1527661591475-527312dd65f5?w=100" },
            new() { Id = Guid.NewGuid(), Name = "Canned & Pantry", NameAr = "معلبات ومؤن", Icon = "https://images.unsplash.com/photo-1584776296944-ab6fb57b0bdd?w=100" },
            new() { Id = Guid.NewGuid(), Name = "Desserts & Sweets", NameAr = "حلويات", Icon = "https://images.unsplash.com/photo-1587314168485-3236d6710814?w=100" }
        };
        await db.Categories.AddRangeAsync(categories);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 4. ORGANIZATIONS (Stores & Charities)
        // -------------------------------------------------------------
        Console.WriteLine("--> [4/15] Seeding Organizations (Stores & Charities)...");
        var organizations = new List<Organization>();
        var verifications = new List<OrganizationVerification>();

        // 4.1 Merchant Stores
        for (int i = 0; i < merchantInfos.Length; i++)
        {
            var info = merchantInfos[i];
            var owner = merchantUsers[i];
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                OwnerId = owner.Id,
                Name = info.Item4,
                Description = $"Premium food retailer saving surplus stock in {info.Item6}.",
                Logo = $"https://images.unsplash.com/photo-1578916171728-46686eac8d58?w=300",
                CoverPhoto = $"https://images.unsplash.com/photo-1604719312566-8912e9227c6a?w=1200",
                Phone = owner.PhoneNumber,
                Email = owner.Email,
                BusinessCategory = info.Item5,
                Governorate = info.Item6 == "Alexandria" ? "Alexandria" : (info.Item6 == "Zayed" || info.Item6 == "Dokki" || info.Item6 == "Mohandessin" ? "Giza" : "Cairo"),
                City = info.Item6,
                Neighborhood = info.Item6,
                Street = info.Item7,
                BuildingNo = $"{random.Next(1, 150)}",
                Latitude = info.Item8,
                Longitude = info.Item9,
                OpeningHours = "{\"Monday\":{\"open\":\"08:00\",\"close\":\"23:00\"},\"Tuesday\":{\"open\":\"08:00\",\"close\":\"23:00\"},\"Wednesday\":{\"open\":\"08:00\",\"close\":\"23:00\"},\"Thursday\":{\"open\":\"08:00\",\"close\":\"23:00\"},\"Friday\":{\"open\":\"09:00\",\"close\":\"23:30\"},\"Saturday\":{\"open\":\"08:00\",\"close\":\"23:00\"},\"Sunday\":{\"open\":\"08:00\",\"close\":\"23:00\"}}",
                VerificationStatus = VerificationStatus.Verified,
                AdminNote = "Verified commercial registry and food safety compliance.",
                AverageRating = Math.Round(4.2 + (random.NextDouble() * 0.7), 1),
                AiAutoDiscountEnabled = true,
                AiAutoDiscountPercent = 25,
                AiAutoDiscountDaysBeforeExpiry = 3,
                AiAutoPricingEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-3)
            };
            organizations.Add(org);

            verifications.Add(new OrganizationVerification
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                VerificationType = UploadDocumentType.CommercialRegistration,
                DocumentUrl = "https://res.cloudinary.com/demo/image/upload/sample_cr.pdf",
                Status = VerificationStatus.Verified,
                ReviewNote = "Valid registry till 2028.",
                ReviewedAt = DateTimeOffset.UtcNow.AddMonths(-3)
            });
            verifications.Add(new OrganizationVerification
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                VerificationType = UploadDocumentType.TaxIdCertificate,
                DocumentUrl = "https://res.cloudinary.com/demo/image/upload/sample_tax.pdf",
                Status = VerificationStatus.Verified,
                ReviewNote = "Tax card active.",
                ReviewedAt = DateTimeOffset.UtcNow.AddMonths(-3)
            });
        }

        // 4.2 Charities
        var charityOrgsList = new List<Organization>();
        for (int i = 0; i < charityInfos.Length; i++)
        {
            var info = charityInfos[i];
            var owner = charityUsers[i];
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                OwnerId = owner.Id,
                Name = info.Item4,
                Description = "Dedicated non-profit organization redistributing meals and food packages to families in need.",
                Logo = "https://images.unsplash.com/photo-1593113598332-cd288d649433?w=300",
                CoverPhoto = "https://images.unsplash.com/photo-1532629345422-7515f3d16bb7?w=1200",
                Phone = owner.PhoneNumber,
                Email = owner.Email,
                BusinessCategory = null,
                Governorate = info.Item5.Contains("Giza") ? "Giza" : "Cairo",
                City = info.Item5.Contains("Giza") ? "Giza" : "Cairo",
                Neighborhood = info.Item5,
                Street = "Main Headquarters Avenue",
                BuildingNo = $"{random.Next(5, 80)}",
                Latitude = info.Item6,
                Longitude = info.Item7,
                OpeningHours = "{\"Monday\":{\"open\":\"09:00\",\"close\":\"17:00\"},\"Tuesday\":{\"open\":\"09:00\",\"close\":\"17:00\"},\"Wednesday\":{\"open\":\"09:00\",\"close\":\"17:00\"},\"Thursday\":{\"open\":\"09:00\",\"close\":\"17:00\"},\"Sunday\":{\"open\":\"09:00\",\"close\":\"17:00\"}}",
                VerificationStatus = VerificationStatus.Verified,
                AdminNote = "Registered under Ministry of Social Solidarity.",
                AverageRating = 4.9,
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-4)
            };
            organizations.Add(org);
            charityOrgsList.Add(org);

            verifications.Add(new OrganizationVerification
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                VerificationType = UploadDocumentType.AssociationCertificate,
                DocumentUrl = "https://res.cloudinary.com/demo/image/upload/sample_ngo_cert.pdf",
                Status = VerificationStatus.Verified,
                ReviewNote = "Official NGO registration approved.",
                ReviewedAt = DateTimeOffset.UtcNow.AddMonths(-4)
            });
        }

        await db.Organizations.AddRangeAsync(organizations);
        await db.OrganizationVerifications.AddRangeAsync(verifications);
        await db.SaveChangesAsync();

        var merchantOrgs = organizations.Where(o => o.BusinessCategory != null).ToList();

        // -------------------------------------------------------------
        // 5. PRODUCTS & PRODUCT IMAGES
        // -------------------------------------------------------------
        Console.WriteLine("--> [5/15] Seeding Products, Images, Price History, and AI Logs...");
        var products = new List<Product>();
        var productImages = new List<ProductImage>();
        var priceHistories = new List<PriceHistory>();
        var aiResults = new List<AIRecognitionResult>();

        var productTemplates = new[]
        {
            ("Artisan Sourdough Loaf", "Freshly baked artisan bread with crispy crust.", "Bakery", 45.0m, 22.5m, "https://images.unsplash.com/photo-1509440159596-0249088772ff?w=600", 3),
            ("French Butter Croissants (Pack of 4)", "Flaky golden croissants made with pure butter.", "Bakery", 60.0m, 30.0m, "https://images.unsplash.com/photo-1555507036-ab1f4038808a?w=600", 2),
            ("Whole Wheat Toast", "Nutritious whole wheat sliced sandwich bread.", "Bakery", 35.0m, 17.5m, "https://images.unsplash.com/photo-1586444248902-2f64eddc13df?w=600", 4),
            ("Cinnamon Rolls (Box of 2)", "Glazed cinnamon pastry with cream cheese frosting.", "Bakery", 50.0m, 25.0m, "https://images.unsplash.com/photo-1509365465985-25d11c17e812?w=600", 1),
            ("Organic Full Cream Milk 1L", "Pasteurized fresh cow milk, 100% natural.", "Dairy & Eggs", 42.0m, 25.0m, "https://images.unsplash.com/photo-1563636619-e9143da7973b?w=600", 4),
            ("Greek Style Yogurt 500g", "Thick creamy plain greek yogurt high in protein.", "Dairy & Eggs", 55.0m, 32.0m, "https://images.unsplash.com/photo-1488477181946-6428a0291777?w=600", 5),
            ("Farm Fresh Eggs (Carton of 30)", "Grade A fresh organic brown eggs.", "Dairy & Eggs", 160.0m, 110.0m, "https://images.unsplash.com/photo-1516467508483-a7212febe31a?w=600", 7),
            ("White Feta Cheese with Olive Oil 250g", "Authentic Mediterranean style white cheese.", "Dairy & Eggs", 70.0m, 42.0m, "https://images.unsplash.com/photo-1559561853-08451507cbe7?w=600", 8),
            ("Organic Bananas 1kg", "Sweet ripe yellow bananas rich in potassium.", "Fruits & Vegetables", 30.0m, 18.0m, "https://images.unsplash.com/photo-1571771894821-ce9b6c11b08e?w=600", 3),
            ("Crisp Gala Apples 1kg", "Imported fresh crunchy gala apples.", "Fruits & Vegetables", 80.0m, 45.0m, "https://images.unsplash.com/photo-1560806887-1e4cd0b6cbd6?w=600", 6),
            ("Fresh Baby Spinach Box 300g", "Pre-washed crisp green salad spinach.", "Fruits & Vegetables", 25.0m, 12.5m, "https://images.unsplash.com/photo-1576045057995-568f588f82fb?w=600", 2),
            ("Vine-Ripened Tomatoes 1.5kg", "Fresh local juicy red cooking tomatoes.", "Fruits & Vegetables", 28.0m, 15.0m, "https://images.unsplash.com/photo-1592924357228-91a4daadcfea?w=600", 4),
            ("Roasted Chicken & Herb Rice Meal", "Chef-prepared half chicken with seasoned basmati rice.", "Prepared Meals", 120.0m, 60.0m, "https://images.unsplash.com/photo-1598515214211-89d3c73ae83b?w=600", 1),
            ("Penne Arrabbiata with Mozzarella", "Fresh italian pasta with spicy tomato basil sauce.", "Prepared Meals", 85.0m, 45.0m, "https://images.unsplash.com/photo-1621996346565-e3d5d6281691?w=600", 2),
            ("Beef Kofta & Tahini Platter", "Grilled egyptian kofta skewers with bread and pickles.", "Prepared Meals", 130.0m, 70.0m, "https://images.unsplash.com/photo-1544025162-d76694265947?w=600", 1),
            ("Cold Pressed Orange Juice 1L", "100% fresh pure valencia orange juice, no sugar added.", "Beverages", 50.0m, 28.0m, "https://images.unsplash.com/photo-1613478223719-2ab802602423?w=600", 3),
            ("Almond Milk Unsweetened 1L", "Plant-based lactose free enriched almond drink.", "Beverages", 90.0m, 55.0m, "https://images.unsplash.com/photo-1550583724-b2692b85b150?w=600", 10),
            ("Fresh Chicken Breast Fillet 1kg", "Tender premium skinless boneless chicken breast.", "Meat & Poultry", 210.0m, 140.0m, "https://images.unsplash.com/photo-1604503468506-a8da13d82791?w=600", 3),
            ("Minced Beef (Low Fat) 500g", "Lean ground beef suitable for burgers and bolognese.", "Meat & Poultry", 190.0m, 125.0m, "https://images.unsplash.com/photo-1588168333986-5078d3ae3976?w=600", 2),
            ("Belgian Chocolate Mousse Cup", "Decadent dark chocolate dessert cup.", "Desserts & Sweets", 45.0m, 22.0m, "https://images.unsplash.com/photo-1541781774459-bb2af2f05b55?w=600", 2),
            ("Mixed Fruit Tartlet", "Crispy pastry crust filled with vanilla custard and fresh fruit.", "Desserts & Sweets", 55.0m, 28.0m, "https://images.unsplash.com/photo-1519869325930-281384150729?w=600", 1)
        };

        foreach (var org in merchantOrgs)
        {
            var shuffled = productTemplates.OrderBy(_ => random.Next()).Take(7).ToList();
            foreach (var item in shuffled)
            {
                var cat = categories.First(c => c.Name == item.Item3);
                var expDays = random.Next(1, 12);
                var qty = random.Next(3, 25);

                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = org.Id,
                    CategoryId = cat.Id,
                    Title = item.Item1,
                    Description = item.Item2,
                    OriginalPrice = item.Item4,
                    DiscountedPrice = item.Item5,
                    QuantityAvailable = qty,
                    ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(expDays)),
                    Status = ProductStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(2, 20))
                };
                products.Add(product);

                productImages.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ImageUrl = item.Item6,
                    DisplayOrder = 0,
                    CreatedAt = product.CreatedAt
                });

                priceHistories.Add(new PriceHistory
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    OldOriginalPrice = item.Item4,
                    OldDiscountedPrice = item.Item4,
                    NewOriginalPrice = item.Item4,
                    NewDiscountedPrice = item.Item5,
                    ChangeReason = "Near expiry automatic price discount adjustment",
                    ChangedBy = org.OwnerId,
                    CreatedAt = product.CreatedAt
                });

                aiResults.Add(new AIRecognitionResult
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    DetectedProduct = product.Title,
                    ExtractedExpiryDate = product.ExpirationDate,
                    ConfidenceScore = Math.Round(0.88 + (random.NextDouble() * 0.11), 2),
                    ExtractedText = $"EXP: {product.ExpirationDate:yyyy-MM-dd} | LOT: {random.Next(100000, 999999)} | {product.Title}",
                    Reviewed = true,
                    CreatedAt = product.CreatedAt
                });
            }
        }

        await db.Products.AddRangeAsync(products);
        await db.ProductImages.AddRangeAsync(productImages);
        await db.PriceHistories.AddRangeAsync(priceHistories);
        await db.AIRecognitionResults.AddRangeAsync(aiResults);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 6. ADDRESSES
        // -------------------------------------------------------------
        Console.WriteLine("--> [6/15] Seeding Customer Saved Addresses...");
        var addresses = new List<Address>();
        foreach (var cust in customerUsers)
        {
            addresses.Add(new Address
            {
                Id = Guid.NewGuid(),
                UserId = cust.Id,
                Street = "El Gezira St",
                BuildingNo = $"{random.Next(1, 40)}",
                Floor = $"{random.Next(1, 10)}",
                ApartmentNo = $"{random.Next(1, 30)}",
                City = "Cairo",
                District = "Zamalek",
                Latitude = 30.0600 + (random.NextDouble() * 0.01),
                Longitude = 31.2200 + (random.NextDouble() * 0.01),
                Notes = "Leave at front desk with security.",
                AddressType = AddressType.Home,
                IsDefault = true,
                CreatedAt = cust.CreatedAt
            });

            if (random.Next(2) == 0)
            {
                addresses.Add(new Address
                {
                    Id = Guid.NewGuid(),
                    UserId = cust.Id,
                    Street = "90th Street North",
                    BuildingNo = $"{random.Next(10, 100)}",
                    Floor = "4",
                    ApartmentNo = "402",
                    City = "Cairo",
                    District = "New Cairo",
                    Latitude = 30.0200 + (random.NextDouble() * 0.02),
                    Longitude = 31.4500 + (random.NextDouble() * 0.02),
                    Notes = "Office building entrance B.",
                    AddressType = AddressType.Company,
                    IsDefault = false,
                    CreatedAt = cust.CreatedAt
                });
            }
        }
        await db.Addresses.AddRangeAsync(addresses);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 7. ORDERS, ORDER ITEMS, & PAYMENTS
        // -------------------------------------------------------------
        Console.WriteLine("--> [7/15] Seeding Orders, Order Items, and Payments...");
        var orders = new List<Order>();
        var orderItems = new List<OrderItem>();
        var payments = new List<Payment>();

        var orderStatuses = new[] { OrderStatus.Completed, OrderStatus.Completed, OrderStatus.Completed, OrderStatus.Confirmed, OrderStatus.Pending };

        for (int i = 0; i < 40; i++)
        {
            var customer = customerUsers[random.Next(customerUsers.Count)];
            var store = merchantOrgs[random.Next(merchantOrgs.Count)];
            var storeProducts = products.Where(p => p.OrganizationId == store.Id).ToList();
            if (!storeProducts.Any()) continue;

            var chosenProducts = storeProducts.OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToList();
            var status = orderStatuses[random.Next(orderStatuses.Length)];

            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = customer.Id,
                TotalAmount = 0,
                OrderStatus = status,
                PaymentStatus = (status == OrderStatus.Completed || status == OrderStatus.Confirmed) ? PaymentStatus.Paid : PaymentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(1, 45))
            };

            decimal total = 0;
            foreach (var prd in chosenProducts)
            {
                var q = random.Next(1, 3);
                var itemTotal = q * prd.DiscountedPrice;
                total += itemTotal;

                orderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = prd.Id,
                    Quantity = q,
                    UnitPrice = prd.DiscountedPrice
                });
            }

            order.TotalAmount = total;
            orders.Add(order);

            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Amount = total,
                    Status = PaymentStatus.Paid,
                    Method = "CreditCard",
                    TransactionReference = $"TXN_{random.Next(1000000, 9999999)}",
                    CreatedAt = order.CreatedAt
                });
            }
        }

        await db.Orders.AddRangeAsync(orders);
        await db.OrderItems.AddRangeAsync(orderItems);
        await db.Payments.AddRangeAsync(payments);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 8. REVIEWS
        // -------------------------------------------------------------
        Console.WriteLine("--> [8/15] Seeding Customer Store Reviews...");
        var reviews = new List<Review>();
        var reviewComments = new[]
        {
            "Great food quality and awesome discount! Everything was fresh.",
            "ممتاز جداً والمنتجات طازجة والتوفير حقيقي، شكراً لكم.",
            "Quick and smooth pickup at the store. Highly recommended!",
            "تجربة رائعة وتطبيق ممتاز لتقليل هدر الطعام والأسعار ممتازة.",
            "The baked bread and croissants tasted fantastic. Will buy again!"
        };

        var completedOrders = orders.Where(o => o.OrderStatus == OrderStatus.Completed).Take(25).ToList();
        foreach (var ord in completedOrders)
        {
            var item = orderItems.FirstOrDefault(oi => oi.OrderId == ord.Id);
            if (item == null) continue;
            var prd = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (prd == null) continue;

            reviews.Add(new Review
            {
                Id = Guid.NewGuid(),
                OrderId = ord.Id,
                UserId = ord.UserId,
                OrganizationId = prd.OrganizationId,
                Rating = random.Next(4, 6),
                Comment = reviewComments[random.Next(reviewComments.Length)],
                CreatedAt = ord.CreatedAt.AddHours(2)
            });
        }
        await db.Reviews.AddRangeAsync(reviews);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 9. FAVORITES
        // -------------------------------------------------------------
        Console.WriteLine("--> [9/15] Seeding User Favorites...");
        var favorites = new List<Favorite>();
        foreach (var cust in customerUsers)
        {
            var favProducts = products.OrderBy(_ => random.Next()).Take(3).ToList();
            foreach (var p in favProducts)
            {
                favorites.Add(new Favorite
                {
                    UserId = cust.Id,
                    ProductId = p.Id,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(1, 30))
                });
            }
        }
        await db.Favorites.AddRangeAsync(favorites);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 10. DONATIONS
        // -------------------------------------------------------------
        Console.WriteLine("--> [10/15] Seeding Surplus Food Donations to Charities...");
        var donations = new List<Donation>();
        for (int i = 0; i < 12; i++)
        {
            var donor = merchantOrgs[random.Next(merchantOrgs.Count)];
            var recipient = charityOrgsList[random.Next(charityOrgsList.Count)];
            var randomProduct = products.First(p => p.OrganizationId == donor.Id);

            donations.Add(new Donation
            {
                Id = Guid.NewGuid(),
                DonorOrganizationId = donor.Id,
                RecipientOrganizationId = recipient.Id,
                ProductId = randomProduct.Id,
                Quantity = random.Next(10, 50),
                Note = "Surplus fresh bakery packages, dairy cups, and bottled juices.",
                Status = "Delivered",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(2, 25))
            });
        }
        await db.Donations.AddRangeAsync(donations);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 11. NOTIFICATIONS
        // -------------------------------------------------------------
        Console.WriteLine("--> [11/15] Seeding User Notifications Feed...");
        var notifications = new List<Notification>();
        foreach (var cust in customerUsers.Take(15))
        {
            notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = cust.Id,
                Title = "Special Surplus Deal Nearby!",
                Body = "Spinneys Supermarket just added 50% discounted items near your location.",
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-random.Next(1, 48))
            });
            notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = cust.Id,
                Title = "Order Confirmed",
                Body = "Your FoodLoop surplus grocery order was confirmed and is being prepared.",
                IsRead = true,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(2, 7))
            });
        }
        await db.Notifications.AddRangeAsync(notifications);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 12. SUPPORT TICKETS & MESSAGES
        // -------------------------------------------------------------
        Console.WriteLine("--> [12/15] Seeding Customer Support Tickets & Conversations...");
        var tickets = new List<SupportTicket>();
        var ticketMessages = new List<TicketMessage>();

        for (int i = 0; i < 10; i++)
        {
            var cust = customerUsers[i];
            var ticket = new SupportTicket
            {
                Id = Guid.NewGuid(),
                UserId = cust.Id,
                Category = "Order Inquiry",
                Status = TicketStatus.Open,
                Priority = TicketPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(1, 10))
            };
            tickets.Add(ticket);

            ticketMessages.Add(new TicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                SenderId = cust.Id,
                Message = "Hello support team, could you please confirm the pickup counter location for my store order?",
                CreatedAt = ticket.CreatedAt
            });

            ticketMessages.Add(new TicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                SenderId = adminUser.Id,
                Message = "Hi! You can proceed to the dedicated FoodLoop pickup counter right at the customer service area.",
                CreatedAt = ticket.CreatedAt.AddHours(2)
            });
        }
        await db.SupportTickets.AddRangeAsync(tickets);
        await db.TicketMessages.AddRangeAsync(ticketMessages);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 13. PRODUCT REPORTS (Disputes)
        // -------------------------------------------------------------
        Console.WriteLine("--> [13/15] Seeding Product Reports & Dispute Queue...");
        var reports = new List<ProductReport>();
        var reportedProducts = products.Take(4).ToList();
        foreach (var p in reportedProducts)
        {
            reports.Add(new ProductReport
            {
                Id = Guid.NewGuid(),
                ProductId = p.Id,
                ReportedBy = customerUsers[random.Next(customerUsers.Count)].Id,
                Reason = "Item packaging had a tear during pickup.",
                Details = "Customer noticed seal damage upon inspection.",
                IsResolved = false,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(1, 5))
            });
        }
        await db.ProductReports.AddRangeAsync(reports);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 14. AUDIT LOGS
        // -------------------------------------------------------------
        Console.WriteLine("--> [14/15] Seeding System Audit Trail Logs...");
        var auditLogs = new List<AuditLog>();
        foreach (var org in merchantOrgs)
        {
            auditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = org.OwnerId,
                OrganizationId = org.Id,
                EventType = "StoreProfileUpdated",
                Title = "Organization Profile Updated",
                Description = $"Updated store details, opening hours, and location coordinates for '{org.Name}'.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(5, 30))
            });
            auditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                OrganizationId = org.Id,
                EventType = "StoreApproved",
                Title = "Organization Approved",
                Description = $"Administrator verified and activated store '{org.Name}'.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(30, 60))
            });
        }
        await db.AuditLogs.AddRangeAsync(auditLogs);
        await db.SaveChangesAsync();

        // -------------------------------------------------------------
        // 15. SUMMARY STATS
        // -------------------------------------------------------------
        Console.WriteLine("\n=======================================================");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" DATA SEEDING COMPLETED SUCCESSFULLY!");
        Console.ResetColor();
        Console.WriteLine("-------------------------------------------------------");
        Console.WriteLine($"• Security Roles:               {roles.Count}");
        Console.WriteLine($"• Users Created:                {users.Count} (1 Admin, 10 Merchants, 5 Charities, 25 Customers)");
        Console.WriteLine($"• Categories:                   {categories.Count}");
        Console.WriteLine($"• Organizations:                {organizations.Count} (10 Stores, 5 Charities)");
        Console.WriteLine($"• Verifications:                {verifications.Count}");
        Console.WriteLine($"• Products Listed:              {products.Count}");
        Console.WriteLine($"• Product Images:               {productImages.Count}");
        Console.WriteLine($"• Price Histories:              {priceHistories.Count}");
        Console.WriteLine($"• AI OCR Scan Logs:             {aiResults.Count}");
        Console.WriteLine($"• Saved Addresses:              {addresses.Count}");
        Console.WriteLine($"• Customer Orders:              {orders.Count}");
        Console.WriteLine($"• Order Line Items:             {orderItems.Count}");
        Console.WriteLine($"• Payments:                     {payments.Count}");
        Console.WriteLine($"• Store Reviews:                {reviews.Count}");
        Console.WriteLine($"• Product Favorites:            {favorites.Count}");
        Console.WriteLine($"• Charity Donations:            {donations.Count}");
        Console.WriteLine($"• Notification Alerts:          {notifications.Count}");
        Console.WriteLine($"• Support Tickets & Messages:   {tickets.Count} tickets / {ticketMessages.Count} messages");
        Console.WriteLine($"• Dispute Reports:              {reports.Count}");
        Console.WriteLine($"• Audit Trail Records:          {auditLogs.Count}");
        Console.WriteLine("=======================================================\n");
    }
}
