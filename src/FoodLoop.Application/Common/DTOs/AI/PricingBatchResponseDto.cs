using System.Collections.Generic;

namespace FoodLoop.Application.Common.DTOs.AI;

public record PricingBatchResponseDto(
    string StoreId,
    IReadOnlyList<PricingDecisionDto> Decisions
);

public record PricingDecisionDto(
    string ProductId,
    double DiscountPercentage, // [0.0, 15.0]
    string Reason, // mandatory, non-empty
    double Confidence, // [0.0, 1.0]
    string ActionRequirement, // "APPROVAL_REQUIRED" | "AUTOMATIC_EXECUTION_ELIGIBLE"
    string ActionReason
);
