using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;

namespace FoodLoop.Infrastructure.Features.Stores;

/// <summary>Shared by every Stores handler that needs "the calling merchant's own store".</summary>
internal static class StoreLookup
{
    public static async Task<Store> FindByOwnerOrThrowAsync(this IUnitOfWork unitOfWork, Guid ownerId, CancellationToken cancellationToken)
    {
        var store = await unitOfWork.Stores.GetByOwnerIdAsync(ownerId, cancellationToken);

        return store ?? throw new NotFoundException(
            "No store was found for this account. Business accounts get a draft store automatically at registration.");
    }

    public static async Task<Store> FindByOwnerEmailOrThrowAsync(this IUnitOfWork unitOfWork, string email, CancellationToken cancellationToken)
    {
        var store = await unitOfWork.Stores.GetByOwnerEmailAsync(email, cancellationToken);

        return store ?? throw new NotFoundException(
            "No store was found for the provided email. Make sure you registered as a Merchant account.");
    }
}
