using FoodLoop.Application.DTOs.Listings;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Queries;

public record GetProductDetailQuery(Guid OwnerId, Guid ProductId) : IRequest<ProductDto>;

