using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/stores/{id} — a single store with all its documents for admin review.</summary>
public record GetStoreForReviewQuery(Guid StoreId) : IRequest<AdminStoreDto>;
