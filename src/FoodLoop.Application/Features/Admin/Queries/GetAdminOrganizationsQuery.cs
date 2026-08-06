using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Domain.Enums;
using MediatR;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetAdminStoresQuery(
    int PageNumber = 1,
    int PageSize = 10,
    VerificationStatus? Status = null) : IRequest<IReadOnlyList<AdminOrganizationDto>>;

