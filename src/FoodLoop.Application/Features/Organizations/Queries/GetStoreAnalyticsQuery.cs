using FoodLoop.Application.DTOs.Organizations;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Organizations.Queries;

public record GetStoreAnalyticsQuery(Guid OwnerId) : IRequest<StoreAnalyticsDto>;
