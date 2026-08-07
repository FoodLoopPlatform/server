using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

/// <summary>
/// Records a merchant's donation of surplus inventory to a charity.
/// Supports POST /stores/me/donations (donation_community_impact screen).
/// </summary>
public class Donation : BaseEntity
{
    public Guid DonorOrganizationId { get; set; }
    public Organization? DonorOrganization { get; set; }

    public Guid RecipientOrganizationId { get; set; }
    public Organization? RecipientOrganization { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Accepted | Delivered
}
