using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
using System;

namespace FoodLoop.Domain.Entities;

public class AiPricingRecommendation : BaseEntity
{
    private decimal _discountPercentage;
    private double _confidence;

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public decimal DiscountPercentage
    {
        get => _discountPercentage;
        set
        {
            if (value < 0m || value > 15m)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "DiscountPercentage must be within the closed interval [0.0, 15.0].");
            }
            _discountPercentage = value;
        }
    }

    public string Reason { get; set; } = string.Empty;

    public double Confidence
    {
        get => _confidence;
        set
        {
            if (value < 0.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Confidence must be within the closed interval [0.0, 1.0].");
            }
            _confidence = value;
        }
    }

    public AiActionRequirement ActionRequirement { get; set; }
    public string ActionReason { get; set; } = string.Empty;

    public AiRecommendationStatus Status { get; set; } = AiRecommendationStatus.Pending;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }

    public Guid? RiskAssessmentId { get; set; }
    public AiRiskAssessment? RiskAssessment { get; set; }

    public string CorrelationId { get; set; } = string.Empty; // MaxLength: 64
    public decimal? SnapshotOriginalPrice { get; set; }
    public int? SnapshotQuantityAvailable { get; set; }
    public ProductStatus? SnapshotProductStatus { get; set; }

    // Parameterless constructor for EF Core serialization
    public AiPricingRecommendation() { }

    public AiPricingRecommendation(
        Guid productId,
        Guid organizationId,
        decimal discountPercentage,
        string reason,
        double confidence,
        AiActionRequirement actionRequirement,
        string actionReason,
        string correlationId,
        AiRecommendationStatus status = AiRecommendationStatus.Pending,
        Guid? riskAssessmentId = null)
    {
        ProductId = productId;
        OrganizationId = organizationId;
        DiscountPercentage = discountPercentage; // Triggers validation setter
        Reason = reason;
        Confidence = confidence; // Triggers validation setter
        ActionRequirement = actionRequirement;
        ActionReason = actionReason;
        CorrelationId = correlationId;
        Status = status;
        RiskAssessmentId = riskAssessmentId;
    }
}
