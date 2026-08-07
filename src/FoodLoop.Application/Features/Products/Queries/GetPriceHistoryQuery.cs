using MediatR;
using FoodLoop.Application.DTOs.Products;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Products.Queries;

/// <summary>GET /stores/me/products/{id}/price-history — price change audit log.</summary>
public record GetPriceHistoryQuery(Guid OwnerId, Guid ProductId) : IRequest<IReadOnlyList<PriceHistoryDto>>;
