namespace FoodLoop.Application.Common.DTOs.AI;

public record MonitoringResponseDto(
    string Route, // "NO_ACTION" | "PRICING"
    string RiskLevel, // "LOW" | "MEDIUM" | "HIGH" | "CRITICAL"
    string Reason,
    double Confidence // [0.0, 1.0]
);
