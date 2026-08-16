using FoodLoop.Application.DTOs.Organizations;
using MediatR;

namespace FoodLoop.Application.Features.Organizations.Queries;

/// <summary>
/// GET /stores/{storeId} — public store profile for the Store Profile screen.
/// Returns store info, reputation metrics, and a page of recent reviews.
/// </summary>
public record GetStoreProfileQuery(
    Guid StoreId,
    int ReviewsPageNumber,
    int ReviewsPageSize
) : IRequest<StoreProfileDto>;
