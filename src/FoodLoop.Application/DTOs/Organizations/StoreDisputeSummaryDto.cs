using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Organizations;

public class StoreDisputeSummaryDto
{
    public int ActiveStrikes { get; set; }
    public int MaxAllowedStrikes { get; set; }
    public string HealthStatus { get; set; } = "Good"; // "Good", "Warning", "Critical", "Suspended"
    public int TotalResolvedDisputes { get; set; }
    public int TotalUnresolvedDisputes { get; set; }
    public List<RepeatProductDisputeDto> RepeatProducts { get; set; } = new();
}

public class RepeatProductDisputeDto
{
    public Guid ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public int ReportCount { get; set; }
}
