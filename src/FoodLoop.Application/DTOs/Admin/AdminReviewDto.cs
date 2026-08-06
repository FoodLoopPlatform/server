using System;

namespace FoodLoop.Application.DTOs.Admin;

public class AdminReviewDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}


