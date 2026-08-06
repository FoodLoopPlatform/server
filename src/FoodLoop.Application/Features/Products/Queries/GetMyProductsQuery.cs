using FoodLoop.Application.DTOs.Products;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Products.Queries;

public record GetMyProductsQuery(
    Guid OwnerId,
    int PageNumber = 1,
    int PageSize = 10,
    Guid? CategoryId = null,
    string? Status = null,
    string? SearchTerm = null) : IRequest<IReadOnlyList<ProductDto>>;


