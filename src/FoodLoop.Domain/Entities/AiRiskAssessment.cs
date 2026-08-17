using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
using System;

namespace FoodLoop.Domain.Entities;

public class AiRiskAssessment : BaseEntity
{
    private double _confidence;

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public AiRiskLevel RiskLevel { get; set; }
    public AiRoute Route { get; set; }
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

    public string? RequestedContext { get; set; } // JSON list of weather/events
    public bool IsPricingStaged { get; set; } = false;
    public string CorrelationId { get; set; } = string.Empty;

    // Parameterless constructor for EF Core serialization
    public AiRiskAssessment() { }

    public AiRiskAssessment(
        Guid productId,
        AiRiskLevel riskLevel,
        AiRoute route,
        string reason,
        double confidence,
        string correlationId = "",
        bool isPricingStaged = false,
        string? requestedContext = null)
    {
        ProductId = productId;
        RiskLevel = riskLevel;
        Route = route;
        Reason = reason;
        Confidence = confidence; // Triggers validation setter
        CorrelationId = correlationId ?? string.Empty;
        IsPricingStaged = isPricingStaged;
        RequestedContext = requestedContext;
    }
}
