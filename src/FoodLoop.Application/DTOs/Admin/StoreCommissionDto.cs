using System;

namespace FoodLoop.Application.DTOs.Admin;

public class StoreCommissionDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public int PlatformCommissionPercent { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCommissionGenerated { get; set; }
    public decimal CommissionWithdrawn { get; set; }
    public decimal OutstandingCommission { get; set; }
}
