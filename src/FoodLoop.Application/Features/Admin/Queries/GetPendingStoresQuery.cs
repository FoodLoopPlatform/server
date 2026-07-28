using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/stores/pending — all stores waiting for admin verification.</summary>
public record GetPendingStoresQuery : IRequest<IReadOnlyList<AdminStoreDto>>;
