using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Persistence;

public class MigrationTests
{
    [Fact]
    public async Task AddAiIntegrationFoundation_mappings_are_valid_and_roundtrip_rows()
    {
        // 1. Establish SQLite InMemory Connection
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        // 2. Setup Context and Create Schema using EF Core Configurations
        using (var context = new ApplicationDbContext(options))
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync(); // Translates model configurations to SQLite schema

            // 3. Populate Dependencies (Category, User, Organization, Product)
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = "Produce",
                NameAr = "خضروات",
                Icon = "fruits"
            };
            context.Categories.Add(category);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "merchant@foodloop.com",
                Email = "merchant@foodloop.com",
                FullName = "Merchant User",
                Status = UserStatus.Active
            };
            context.Users.Add(user);

            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                OwnerId = user.Id,
                Name = "Healthy Foods Store",
                VerificationStatus = VerificationStatus.Verified,
                AiOperatingMode = AiOperatingMode.Assisted
            };
            context.Organizations.Add(organization);

            var product = new Product
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                CategoryId = category.Id,
                Title = "Organic Avocado",
                OriginalPrice = 50.00m,
                DiscountedPrice = 40.00m,
                QuantityAvailable = 10,
                ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                Status = ProductStatus.Active
            };
            context.Products.Add(product);

            await context.SaveChangesAsync();

            // 4. Test AiRiskAssessment Row Round-Trip
            var riskAssessment = new AiRiskAssessment(
                productId: product.Id,
                riskLevel: AiRiskLevel.CRITICAL,
                route: AiRoute.PRICING,
                reason: "Avocados are expiring tomorrow",
                confidence: 0.98,
                requestedContext: "{\"local_events\":\"festival\"}"
            );
            context.AiRiskAssessments.Add(riskAssessment);

            // 5. Test AiPricingRecommendation Row Round-Trip
            var pricingRecommendation = new AiPricingRecommendation(
                productId: product.Id,
                organizationId: organization.Id,
                discountPercentage: 15.00m,
                reason: "Immediate expiry threshold reached",
                confidence: 0.95,
                actionRequirement: AiActionRequirement.AUTOMATIC_EXECUTION_ELIGIBLE,
                actionReason: "Eligible for auto pricing",
                correlationId: "AI-CORRELATION-ID-9999",
                status: AiRecommendationStatus.Pending
            )
            {
                ApprovedBy = user.Id
            };
            context.AiPricingRecommendations.Add(pricingRecommendation);

            await context.SaveChangesAsync();
        }

        // 6. Read back from database to verify round-trip persistence and property conversions
        using (var context = new ApplicationDbContext(options))
        {
            var savedRisk = await context.AiRiskAssessments.FirstOrDefaultAsync();
            savedRisk.Should().NotBeNull();
            savedRisk!.RiskLevel.Should().Be(AiRiskLevel.CRITICAL);
            savedRisk.Route.Should().Be(AiRoute.PRICING);
            savedRisk.Reason.Should().Be("Avocados are expiring tomorrow");
            savedRisk.Confidence.Should().Be(0.98);
            savedRisk.RequestedContext.Should().Be("{\"local_events\":\"festival\"}");

            var savedPricing = await context.AiPricingRecommendations.FirstOrDefaultAsync();
            savedPricing.Should().NotBeNull();
            savedPricing!.DiscountPercentage.Should().Be(15.00m);
            savedPricing.Reason.Should().Be("Immediate expiry threshold reached");
            savedPricing.Confidence.Should().Be(0.95);
            savedPricing.ActionRequirement.Should().Be(AiActionRequirement.AUTOMATIC_EXECUTION_ELIGIBLE);
            savedPricing.Status.Should().Be(AiRecommendationStatus.Pending);
            savedPricing.ApprovedBy.Should().Be(context.Users.First().Id);
            savedPricing.CorrelationId.Should().Be("AI-CORRELATION-ID-9999");

            var savedOrg = await context.Organizations.FirstOrDefaultAsync();
            savedOrg.Should().NotBeNull();
            savedOrg!.AiOperatingMode.Should().Be(AiOperatingMode.Assisted);
        }
    }
}
