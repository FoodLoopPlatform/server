using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Commands;

public class UploadOrganizationDocumentCommandHandler : IRequestHandler<UploadOrganizationDocumentCommand, OrganizationDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ILocalizationService _loc;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;

    public UploadOrganizationDocumentCommandHandler(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        ILocalizationService loc,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _loc = loc;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    public async Task<OrganizationDto> Handle(UploadOrganizationDocumentCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerEmailOrThrowAsync(
            command.OwnerEmail,
            _loc["OrganizationNotFoundByEmail"],
            cancellationToken);

        var owner = await _userManager.FindByIdAsync(organization.OwnerId.ToString());
        if (owner == null)
        {
            throw new ArgumentException(_loc["OwnerNotFound"] ?? "Owner user not found.");
        }

        var isCharity = await _userManager.IsInRoleAsync(owner, AppRole.Charity);

        // Validate allowed document types based on role
        if (isCharity)
        {
            var allowedCharityTypes = new[] { UploadDocumentType.AssociationCertificate, UploadDocumentType.CharityBylaws, UploadDocumentType.BoardOfDirectorsList };
            if (!allowedCharityTypes.Contains(command.VerificationType))
            {
                throw new ArgumentException(_loc["InvalidCharityDocumentType"] ?? "Charities can only upload AssociationCertificate, CharityBylaws, or BoardOfDirectorsList.");
            }
        }
        else
        {
            var allowedMerchantTypes = new[] { UploadDocumentType.CommercialRegistration, UploadDocumentType.TaxIdCertificate, UploadDocumentType.OrganizationFacilityPhoto };
            if (!allowedMerchantTypes.Contains(command.VerificationType))
            {
                throw new ArgumentException(_loc["InvalidOrganizationDocumentType"] ?? "Organizations can only upload CommercialRegistration, TaxIdCertificate, or OrganizationFacilityPhoto.");
            }
        }

        var documentUrl = await _fileStorage.SaveAsync(command.File, $"organizations/{organization.Id}", cancellationToken);

        // Replace any prior upload of the same type rather than accumulating duplicates.
        var existing = organization.Verifications.FirstOrDefault(v => v.VerificationType == command.VerificationType);
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
                VerificationType = command.VerificationType,
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

        await _auditLogService.LogAsync(
            organization.OwnerId,
            organization.Id,
            "DocumentUploaded",
            "Document Uploaded",
            $"Uploaded {command.VerificationType} document.",
            null,
            cancellationToken);

        return organization.ToDto();
    }
}



