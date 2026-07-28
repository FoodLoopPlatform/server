using FoodLoop.Application.DTOs.Stores;
using MediatR;

namespace FoodLoop.Application.Features.Stores.Commands;

/// <summary>PATCH /stores/me — updates the store's name, description, category, and logo.</summary>
public record UpdateStoreProfileCommand(Guid OwnerId, UpdateStoreProfileRequest Request) : IRequest<StoreDto>;
