using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Products;

public class RiskAnalysisDto
{
    public RiskSummaryDto Summary { get; set; } = new();
    public List<RiskProductDto> Critical { get; set; } = new(); // expires <= 1 day
    public List<RiskProductDto> High { get; set; } = new();     // expires <= 3 days
    public List<RiskProductDto> Medium { get; set; } = new();   // expires <= 7 days
    public List<RiskProductDto> Low { get; set; } = new();      // expires > 7 days
}

public class RiskSummaryDto
{
    public int TotalActiveProducts { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public decimal TotalAtRiskValue { get; set; }
}

public class RiskProductDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public decimal DiscountedPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public int QuantityAvailable { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public decimal PotentialLoss { get; set; }
}
