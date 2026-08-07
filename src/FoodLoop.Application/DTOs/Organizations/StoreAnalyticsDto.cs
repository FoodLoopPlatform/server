using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Organizations;

public class StoreAnalyticsDto
{
    public decimal Revenue { get; set; }
    public int OrdersCount { get; set; }
    public decimal SavingsImpact { get; set; }
    public List<TopProductDto> TopProducts { get; set; } = new();
}

public class TopProductDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public int QuantitySold { get; set; }
    public decimal RevenueGenerated { get; set; }
}
