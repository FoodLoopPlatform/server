using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetAdminProductsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Status = null,
    Guid? StoreId = null) : IRequest<IReadOnlyList<AdminProductDto>>;
