using MediatR;
using FoodLoop.Application.DTOs.Products;
using System;

namespace FoodLoop.Application.Features.Products.Queries;

/// <summary>GET /stores/me/products/risk-analysis — expiry risk report grouped by risk level.</summary>
public record GetRiskAnalysisQuery(Guid OwnerId) : IRequest<RiskAnalysisDto>;
