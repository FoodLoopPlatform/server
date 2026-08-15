using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Common.Interfaces;

public interface IOrganizationRepository : IRepository<Organization>
{
    /// <summary>The merchant's own organization, with its verification documents loaded â€”
    /// what StoreService needs for every onboarding-wizard step.</summary>
    Task<Organization?> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Looks up a organization by the owner's email address. Used when the caller
    /// is not yet authenticated (e.g. document upload during verification onboarding).</summary>
    Task<Organization?> GetByOwnerEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>All organizations with a given verification status, with verifications loaded.
    /// Used by admin to list organizations pending review.</summary>
    Task<IReadOnlyList<Organization>> GetByVerificationStatusAsync(VerificationStatus status, CancellationToken cancellationToken = default);

    /// <summary>A single organization by its own id, with verifications loaded â€” for admin review.</summary>
    Task<Organization?> GetByIdWithVerificationsAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>A single organization by id with reviews loaded â€" for public store profile.</summary>
    Task<Organization?> GetByIdWithReviewsAsync(Guid organizationId, CancellationToken cancellationToken = default);
}

