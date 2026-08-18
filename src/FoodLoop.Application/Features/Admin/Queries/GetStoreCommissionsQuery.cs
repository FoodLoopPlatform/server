using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetStoreCommissionsQuery : IRequest<IReadOnlyList<StoreCommissionDto>>;
