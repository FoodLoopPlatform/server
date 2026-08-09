using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FoodLoop.DbTool;

public static class DataVerifier
{
    public static async Task<bool> VerifyDatabaseAsync(ApplicationDbContext db)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n=======================================================");
        Console.WriteLine("    FOODLOOP DATABASE INTEGRITY & DATA VERIFICATION   ");
        Console.WriteLine("=======================================================\n");
        Console.ResetColor();

        bool allMatched = true;

        // 1. Roles
        var roleCount = await db.Roles.CountAsync();
        Console.WriteLine($"[1] Security Roles in DB: {roleCount} (Expected: 4)");
        if (roleCount < 4) allMatched = false;

        // 2. Users
        var totalUsers = await db.Users.CountAsync();
        var adminCount = await (from u in db.Users
                                join ur in db.UserRoles on u.Id equals ur.UserId
                                join r in db.Roles on ur.RoleId equals r.Id
                                where r.Name == AppRole.Admin
                                select u).CountAsync();

        var merchantCount = await (from u in db.Users
                                  join ur in db.UserRoles on u.Id equals ur.UserId
                                  join r in db.Roles on ur.RoleId equals r.Id
                                  where r.Name == AppRole.Merchant
                                  select u).CountAsync();

        var charityCount = await (from u in db.Users
                                 join ur in db.UserRoles on u.Id equals ur.UserId
                                 join r in db.Roles on ur.RoleId equals r.Id
                                 where r.Name == AppRole.Charity
                                 select u).CountAsync();

        var customerCount = await (from u in db.Users
                                  join ur in db.UserRoles on u.Id equals ur.UserId
                                  join r in db.Roles on ur.RoleId equals r.Id
                                  where r.Name == AppRole.Customer
                                  select u).CountAsync();

        Console.WriteLine($"[2] Users in DB: Total={totalUsers}");
        Console.WriteLine($"    • Admins:     {adminCount} (Expected: >= 1)");
        Console.WriteLine($"    • Merchants:  {merchantCount} (Expected: >= 10)");
        Console.WriteLine($"    • Charities:  {charityCount} (Expected: >= 5)");
        Console.WriteLine($"    • Customers:  {customerCount} (Expected: >= 25)");

        if (adminCount < 1 || merchantCount < 10 || charityCount < 5 || customerCount < 25)
        {
            allMatched = false;
        }

        // 3. Password Hash Verification
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@foodloop.com");
        var adminPwOk = adminUser != null && passwordHasher.VerifyHashedPassword(adminUser, adminUser.PasswordHash!, "Admin@123") == PasswordVerificationResult.Success;
        Console.WriteLine($"    • Admin Password Valid ('Admin@123'): {(adminPwOk ? "YES [OK]" : "NO [FAILED]")}");

        var spinneysUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "merchant.spinneys@example.com");
        var merchantPwOk = spinneysUser != null && passwordHasher.VerifyHashedPassword(spinneysUser, spinneysUser.PasswordHash!, "Password@123") == PasswordVerificationResult.Success;
        Console.WriteLine($"    • Merchant Password Valid ('Password@123'): {(merchantPwOk ? "YES [OK]" : "NO [FAILED]")}");

        var foodbankUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "charity.foodbank@example.com");
        var charityPwOk = foodbankUser != null && passwordHasher.VerifyHashedPassword(foodbankUser, foodbankUser.PasswordHash!, "Password@123") == PasswordVerificationResult.Success;
        Console.WriteLine($"    • Charity Password Valid ('Password@123'): {(charityPwOk ? "YES [OK]" : "NO [FAILED]")}");

        var ahmedUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "ahmed.hassan@example.com");
        var custPwOk = ahmedUser != null && passwordHasher.VerifyHashedPassword(ahmedUser, ahmedUser.PasswordHash!, "Password@123") == PasswordVerificationResult.Success;
        Console.WriteLine($"    • Customer Password Valid ('Password@123'): {(custPwOk ? "YES [OK]" : "NO [FAILED]")}");

        if (!adminPwOk || !merchantPwOk || !charityPwOk || !custPwOk) allMatched = false;

        // 4. Categories
        var catCount = await db.Categories.CountAsync(c => !c.IsDeleted);
        Console.WriteLine($"[3] Categories in DB: {catCount} (Expected: 8)");
        if (catCount < 8) allMatched = false;

        // 5. Organizations
        var orgCount = await db.Organizations.CountAsync(o => !o.IsDeleted);
        var storeCount = await db.Organizations.CountAsync(o => !o.IsDeleted && o.BusinessCategory != null);
        var charityOrgCount = await db.Organizations.CountAsync(o => !o.IsDeleted && o.BusinessCategory == null);
        Console.WriteLine($"[4] Organizations in DB: Total={orgCount} (Stores={storeCount}, Charities={charityOrgCount})");
        if (storeCount < 10 || charityOrgCount < 5) allMatched = false;

        // 6. Products & Images
        var prodCount = await db.Products.CountAsync(p => !p.IsDeleted);
        var imgCount = await db.ProductImages.CountAsync();
        var priceHistCount = await db.PriceHistories.CountAsync();
        var aiCount = await db.AIRecognitionResults.CountAsync();
        Console.WriteLine($"[5] Products in DB: {prodCount} (Images: {imgCount}, Price Histories: {priceHistCount}, AI OCR Logs: {aiCount})");
        if (prodCount < 70) allMatched = false;

        // 7. Addresses
        var addrCount = await db.Addresses.CountAsync();
        Console.WriteLine($"[6] Saved Addresses in DB: {addrCount} (Expected: >= 35)");
        if (addrCount < 25) allMatched = false;

        // 8. Orders, Items, Payments
        var orderCount = await db.Orders.CountAsync();
        var itemCount = await db.OrderItems.CountAsync();
        var paymentCount = await db.Payments.CountAsync();
        Console.WriteLine($"[7] Orders in DB: {orderCount} (Items: {itemCount}, Payments: {paymentCount})");
        if (orderCount < 40) allMatched = false;

        // 9. Reviews & Favorites
        var revCount = await db.Reviews.CountAsync();
        var favCount = await db.Favorites.CountAsync();
        Console.WriteLine($"[8] Reviews: {revCount}, Favorites: {favCount}");

        // 10. Donations, Notifications, Tickets
        var donationCount = await db.Donations.CountAsync();
        var notifCount = await db.Notifications.CountAsync();
        var ticketCount = await db.SupportTickets.CountAsync();
        var msgCount = await db.TicketMessages.CountAsync();
        var reportCount = await db.ProductReports.CountAsync();
        var auditCount = await db.AuditLogs.CountAsync();

        Console.WriteLine($"[9] Donations: {donationCount}, Notifications: {notifCount}");
        Console.WriteLine($"[10] Tickets: {ticketCount} (Messages: {msgCount}), Disputes: {reportCount}, Audit Logs: {auditCount}");

        Console.WriteLine("\n-------------------------------------------------------");
        if (allMatched)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" --> ALL DATABASE ENTITIES ARE FULLY MATCHED & VALIDATED!");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" --> Some counts are lower than expected (database may need re-seeding).");
            Console.ResetColor();
        }
        Console.WriteLine("=======================================================\n");

        return allMatched;
    }
}
