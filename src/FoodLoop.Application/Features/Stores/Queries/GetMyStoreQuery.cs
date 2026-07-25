using FoodLoop.Application.DTOs.Stores;
using MediatR;

namespace FoodLoop.Application.Features.Stores.Queries;

/// <summary>GET /stores/me — the calling merchant's own store, including uploaded documents.</summary>
public record GetMyStoreQuery(Guid OwnerId) : IRequest<StoreDto>;
