using System;

namespace FoodLoop.Application.DTOs.Organizations;

public class DonationDto
{
    public Guid Id { get; set; }
    public Guid DonorOrganizationId { get; set; }
    public string DonorName { get; set; } = string.Empty;
    public Guid RecipientOrganizationId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
