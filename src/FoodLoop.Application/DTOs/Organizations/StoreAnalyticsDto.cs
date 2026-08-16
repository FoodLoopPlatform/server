using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Organizations;

public class StoreAnalyticsDto
{
    /// <summary>The requested period: "today" | "week" | "month" | "all"</summary>
    public string Period { get; set; } = "all";
    public decimal Revenue { get; set; }
    public int OrdersCount { get; set; }
    public decimal SavingsImpact { get; set; }
    
    // Financials
    public decimal AverageOrderValue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal DonatedValue { get; set; }

    // Order status counters
    public int PendingOrdersCount { get; set; }
    public int ConfirmedOrdersCount { get; set; }
    public int PreparingOrdersCount { get; set; }
    public int ReadyForPickupOrdersCount { get; set; }
    public int CompletedOrdersCount { get; set; }
    public int CancelledOrdersCount { get; set; }

    // Products
    public int TotalProductsCount { get; set; }
    public int OutOfStockProductsCount { get; set; }
    public int ExpiringSoonProductsCount { get; set; }

    // Disputes
    public int TotalDisputesCount { get; set; }
    public int UnresolvedDisputesCount { get; set; }
    public int ResolvedDisputesCount { get; set; }

    public List<TopProductDto> TopProducts { get; set; } = new();
}

public class TopProductDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal RevenueGenerated { get; set; }
}
