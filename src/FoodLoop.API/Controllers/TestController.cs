using FoodLoop.API.Common;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("test")]
[Route("api/test")]
[AllowAnonymous]
public class TestController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IServiceProvider _serviceProvider;

    public TestController(ApplicationDbContext context, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        _context = context;
        _env = env;
        _serviceProvider = serviceProvider;
    }

    [HttpPost("reset-db")]
    public async Task<IActionResult> ResetDb(CancellationToken cancellationToken)
    {
        // Safety check: Only allow this in Development or Testing environment
        if (!_env.IsDevelopment() && _env.EnvironmentName != "Testing")
        {
            return StatusCode(403, ApiResponse<string>.Fail("Database reset is only allowed in Development or Testing environments."));
        }

        try
        {
            // Disable all constraints and delete all data in correct dependency order
            await _context.Database.ExecuteSqlRawAsync(@"
                SET QUOTED_IDENTIFIER ON;
                SET ANSI_NULLS ON;
                EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all';

                DELETE FROM [AuditLogs];
                DELETE FROM [Notifications];
                DELETE FROM [OrderItems];
                DELETE FROM [Orders];
                DELETE FROM [AIRecognitionResults];
                DELETE FROM [ProductImages];
                DELETE FROM [Products];
                DELETE FROM [OrganizationVerifications];
                DELETE FROM [Organizations];
                DELETE FROM [TicketMessages];
                DELETE FROM [SupportTickets];
                DELETE FROM [Reviews];
                DELETE FROM [Payments];
                DELETE FROM [Favorites];
                DELETE FROM [Addresses];
                DELETE FROM [UserRoles];
                DELETE FROM [UserClaims];
                DELETE FROM [UserLogins];
                DELETE FROM [UserTokens];
                DELETE FROM [RoleClaims];
                DELETE FROM [RefreshTokens];
                DELETE FROM [Users];
                DELETE FROM [Roles];
                DELETE FROM [Categories];

                EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all';
            ", cancellationToken);

            // Re-seed the identity database
            await IdentitySeeder.SeedAsync(_serviceProvider);

            return Ok(ApiResponse<string>.Ok("Database reset and re-seeded successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail($"Failed to reset database: {ex.Message}"));
        }
    }
}
