namespace FoodLoop.Application.DTOs.Admin;

public class AnalyticsSummaryDto
{
    public UserMetricsDto Users { get; set; } = new();
    public StoreMetricsDto Organizations { get; set; } = new();
    public ProductMetricsDto Products { get; set; } = new();
    public OrderMetricsDto Orders { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalFoodSavings { get; set; }
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

