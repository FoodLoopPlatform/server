using FoodLoop.Application.DTOs.Stores;
using MediatR;

namespace FoodLoop.Application.Features.Stores.Commands;

/// <summary>PATCH /stores/me/location — step 2's location fields (business_verification_location).</summary>
public record UpdateStoreLocationCommand(Guid OwnerId, UpdateStoreLocationRequest Request) : IRequest<StoreDto>;
