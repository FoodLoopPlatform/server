using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Admin;

public class AnalyticsSummaryDto
{
    // Environmental & Financial Impact Metrics (Top Cards)
    public double FoodWastePreventedKg { get; set; }
    public double Co2EmissionsSavedKg { get; set; }
    public decimal FinancialValueRecovered { get; set; }
    public double DisputeRatePercentage { get; set; }

    // Core System Entity Counts
    public UserMetricsDto Users { get; set; } = new();
    public StoreMetricsDto Organizations { get; set; } = new();
    public ProductMetricsDto Products { get; set; } = new();
    public OrderMetricsDto Orders { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalFoodSavings { get; set; }

    // Analytics Breakdown Lists (Stores, Charities, Categories)
    public IReadOnlyList<TopStoreAnalyticsDto> TopStores { get; set; } = new List<TopStoreAnalyticsDto>();
    public IReadOnlyList<TopCharityAnalyticsDto> TopCharities { get; set; } = new List<TopCharityAnalyticsDto>();
    public IReadOnlyList<CategoryImpactAnalyticsDto> CategoryBreakdown { get; set; } = new List<CategoryImpactAnalyticsDto>();
    public IReadOnlyList<MonthlyImpactTrendDto> MonthlyTrends { get; set; } = new List<MonthlyImpactTrendDto>();

    // Smart AI Opportunity Banner
    public AiDemandOpportunityDto? AiOpportunity { get; set; }

    // System Health & Audit KPIs (Bottom cards of Audit Log page)
    public SystemAuditSummaryDto SystemAudit { get; set; } = new();
}

public class TopStoreAnalyticsDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int RescuedBagsCount { get; set; }
    public double FoodSavedKg { get; set; }
    public decimal TotalSalesValue { get; set; }
}

public class TopCharityAnalyticsDto
{
    public Guid CharityId { get; set; }
    public string CharityName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public double DonatedFoodKg { get; set; }
    public int SupportBoxesCount { get; set; }
    public int TotalDonationsCount { get; set; }
}

public class CategoryImpactAnalyticsDto
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public int RescuedItemsCount { get; set; }
    public double FoodSavedKg { get; set; }
    public decimal TotalFinancialValue { get; set; }
    public double PercentageOfTotal { get; set; }
}

public class MonthlyImpactTrendDto
{
    public string Month { get; set; } = string.Empty;
    public int Year { get; set; }
    public double WastePreventedKg { get; set; }
    public decimal FinancialSavings { get; set; }
    public int OrdersCount { get; set; }
}

public class AiDemandOpportunityDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public double WastePercentage { get; set; }
    public string ActionHint { get; set; } = string.Empty;
}

public class SystemAuditSummaryDto
{
    public int ActiveSessionsCount { get; set; }
    public int AiDecisions24hCount { get; set; }
    public int ReportedIncidentsCount { get; set; }
    public string SystemHealth { get; set; } = "تشغيل مستقر PostgreSQL / .NET API";
}

public class UserMetricsDto
{
    public int Total { get; set; }
    public int Customers { get; set; }
    public int Merchants { get; set; }
    public int Charities { get; set; }
    public int Admins { get; set; }
}

public class StoreMetricsDto
{
    public int Total { get; set; }
    public int Unverified { get; set; }
    public int Pending { get; set; }
    public int Verified { get; set; }
    public int Rejected { get; set; }
}

public class ProductMetricsDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int SoldOut { get; set; }
    public int Expired { get; set; }
}

public class OrderMetricsDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
}
