using System;

namespace FoodLoop.Application.DTOs.Admin;

public class DisputeDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public Guid ReportedBy { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public bool IsResolved { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
