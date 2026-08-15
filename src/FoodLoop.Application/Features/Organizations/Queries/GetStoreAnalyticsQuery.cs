using FoodLoop.Application.DTOs.Organizations;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Organizations.Queries;

/// <summary>
/// Period values: "today" | "week" | "month" | "all" (default: "all")
/// </summary>
public record GetStoreAnalyticsQuery(Guid OwnerId, string Period = "all") : IRequest<StoreAnalyticsDto>;
