using FoodLoop.Application.DTOs.Products;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Queries;

public record GetStorePricingOverviewQuery(Guid OwnerId) : IRequest<StorePricingOverviewDto>;
