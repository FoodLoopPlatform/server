using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FoodLoop.DbTool;

public static class DataCleaner
{
    public static async Task ResetDatabaseAsync(ApplicationDbContext db)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[1/3] Disabling database constraints...");
        Console.ResetColor();

        await db.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[2/3] Wiping all data from tables...");
        Console.ResetColor();

        var tableCleanupSql = @"
            DELETE FROM [TicketMessages];
            DELETE FROM [SupportTickets];
            DELETE FROM [ProductReports];
            DELETE FROM [AIRecognitionResults];
            DELETE FROM [PriceHistories];
            DELETE FROM [AuditLogs];
            DELETE FROM [Reviews];
            DELETE FROM [Payments];
            DELETE FROM [OrderItems];
            DELETE FROM [Orders];
            DELETE FROM [Favorites];
            DELETE FROM [ProductImages];
            DELETE FROM [Donations];
            DELETE FROM [Products];
            DELETE FROM [Categories];
            DELETE FROM [OrganizationVerifications];
            DELETE FROM [Organizations];
            DELETE FROM [Addresses];
            DELETE FROM [Notifications];
            DELETE FROM [RefreshTokens];
            DELETE FROM [UserTokens];
            DELETE FROM [UserRoles];
            DELETE FROM [UserLogins];
            DELETE FROM [UserClaims];
            DELETE FROM [RoleClaims];
            DELETE FROM [Users];
            DELETE FROM [Roles];
        ";

        await db.Database.ExecuteSqlRawAsync(tableCleanupSql);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[3/3] Re-enabling database constraints...");
        Console.ResetColor();

        await db.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" Database tables have been successfully wiped and reset.\n");
        Console.ResetColor();
    }
}
