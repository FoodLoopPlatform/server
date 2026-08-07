using MediatR;
using FoodLoop.Application.DTOs.Products;
using System;

namespace FoodLoop.Application.Features.Products.Queries;

/// <summary>GET /marketplace/products/{id} — public product detail page.</summary>
public record GetMarketplaceProductDetailQuery(Guid ProductId) : IRequest<MarketplaceProductDto>;
