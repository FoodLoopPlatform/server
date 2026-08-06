using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;

namespace FoodLoop.Infrastructure.Features.Organizations;

/// <summary>Shared by every Organizations handler that needs "the calling merchant's own organization".</summary>
internal static class OrganizationLookup
{
    public static async Task<Organization> FindByOwnerOrThrowAsync(
        this IUnitOfWork unitOfWork,
        Guid ownerId,
        string notFoundMessage,
        CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.Organizations.GetByOwnerIdAsync(ownerId, cancellationToken);
        return organization ?? throw new NotFoundException(notFoundMessage);
    }

    public static async Task<Organization> FindByOwnerEmailOrThrowAsync(
        this IUnitOfWork unitOfWork,
        string email,
        string notFoundMessage,
        CancellationToken cancellationToken)
    {
        var organization = await unitOfWork.Organizations.GetByOwnerEmailAsync(email, cancellationToken);
        return organization ?? throw new NotFoundException(notFoundMessage);
    }
}


