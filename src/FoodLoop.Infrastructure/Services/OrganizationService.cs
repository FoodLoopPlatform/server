using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Services;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using System.Linq;

namespace FoodLoop.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrganizationService(IUnitOfWork unitOfWork, IFileStorageService fileStorage, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _userManager = userManager;
    }

    public async Task<OrganizationDto> GetMyOrganizationAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var organization = await FindOrganizationOrThrowAsync(ownerId, cancellationToken);
        return ToDto(organization);
    }

    public async Task<OrganizationDto> UpdateLocationAsync(Guid ownerId, UpdateOrganizationLocationRequest request, CancellationToken cancellationToken = default)
    {
        var organization = await FindOrganizationOrThrowAsync(ownerId, cancellationToken);

        organization.Governorate = request.Governorate;
        organization.City = request.City;
        organization.Neighborhood = request.Neighborhood;
        organization.Street = request.Street;
        organization.Latitude = request.Latitude;
        organization.Longitude = request.Longitude;
        organization.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(organization);
    }

    public async Task<OrganizationDto> UploadDocumentAsync(Guid ownerId, UploadDocumentType verificationType, FileUploadRequest file, CancellationToken cancellationToken = default)
    {
        var organization = await FindOrganizationOrThrowAsync(ownerId, cancellationToken);

        var owner = await _userManager.FindByIdAsync(organization.OwnerId.ToString());
        if (owner == null)
        {
            throw new NotFoundException("Owner user not found.");
        }

        var isCharity = await _userManager.IsInRoleAsync(owner, AppRole.Charity);

        // Validate allowed document types based on role
        if (isCharity)
        {
            var allowedCharityTypes = new[] { UploadDocumentType.AssociationCertificate, UploadDocumentType.CharityBylaws, UploadDocumentType.BoardOfDirectorsList };
            if (!allowedCharityTypes.Contains(verificationType))
            {
                throw new ArgumentException("Charities can only upload AssociationCertificate, CharityBylaws, or BoardOfDirectorsList.");
            }
        }
        else
        {
            var allowedMerchantTypes = new[] { UploadDocumentType.CommercialRegistration, UploadDocumentType.TaxIdCertificate, UploadDocumentType.OrganizationFacilityPhoto };
            if (!allowedMerchantTypes.Contains(verificationType))
            {
                throw new ArgumentException("Organizations can only upload CommercialRegistration, TaxIdCertificate, or OrganizationFacilityPhoto.");
            }
        }

        var documentUrl = await _fileStorage.SaveAsync(file, $"organizations/{organization.Id}", cancellationToken);

        // Replace any prior upload of the same type rather than accumulating duplicates.
        var existing = organization.Verifications.FirstOrDefault(v => v.VerificationType == verificationType);
        if (existing != null)
        {
            existing.DocumentUrl = documentUrl;
            existing.Status = VerificationStatus.Pending;
            existing.ReviewedAt = null;
            existing.ReviewedBy = null;
        }
        else
        {
            var organizationVerification = new OrganizationVerification
            {
                OrganizationId = organization.Id,
                VerificationType = verificationType,
                DocumentUrl = documentUrl,
                Status = VerificationStatus.Pending,
            };
            _unitOfWork.Repository<OrganizationVerification>().Add(organizationVerification);
            if (!organization.Verifications.Contains(organizationVerification))
            {
                organization.Verifications.Add(organizationVerification);
            }
        }

        // Determine required document types
        var requiredTypes = isCharity
            ? new[] { UploadDocumentType.AssociationCertificate, UploadDocumentType.CharityBylaws, UploadDocumentType.BoardOfDirectorsList }
            : new[] { UploadDocumentType.CommercialRegistration, UploadDocumentType.TaxIdCertificate, UploadDocumentType.OrganizationFacilityPhoto };

        if (requiredTypes.All(t => organization.Verifications.Any(v => v.VerificationType == t)))
        {
            organization.VerificationStatus = VerificationStatus.Pending;
        }

        organization.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(organization);
    }

    private async Task<Organization> FindOrganizationOrThrowAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.Organizations.GetByOwnerIdAsync(ownerId, cancellationToken);

        return organization ?? throw new NotFoundException(
            "No organization was found for this account. Business accounts get a draft organization automatically at registration.");
    }

    private static OrganizationDto ToDto(Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        BusinessCategory = organization.BusinessCategory,
        Governorate = organization.Governorate,
        City = organization.City,
        Neighborhood = organization.Neighborhood,
        Street = organization.Street,
        Latitude = organization.Latitude,
        Longitude = organization.Longitude,
        VerificationStatus = organization.VerificationStatus.ToString(),
        Documents = organization.Verifications.Select(v => new OrganizationDocumentDto
        {
            Id = v.Id,
            VerificationType = v.VerificationType.ToString(),
            DocumentUrl = v.DocumentUrl,
            Status = v.Status.ToString(),
        }).ToArray(),
    };
}


