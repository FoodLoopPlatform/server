using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Features.Stores.Commands;
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

namespace FoodLoop.Infrastructure.Features.Stores;

public class UploadStoreDocumentCommandHandler : IRequestHandler<UploadStoreDocumentCommand, StoreDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ILocalizationService _loc;
    private readonly UserManager<ApplicationUser> _userManager;

    public UploadStoreDocumentCommandHandler(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        ILocalizationService loc,
        UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _loc = loc;
        _userManager = userManager;
    }

    public async Task<StoreDto> Handle(UploadStoreDocumentCommand command, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerEmailOrThrowAsync(
            command.OwnerEmail,
            _loc["StoreNotFoundByEmail"],
            cancellationToken);

        var owner = await _userManager.FindByIdAsync(store.OwnerId.ToString());
        if (owner == null)
        {
            throw new ArgumentException(_loc["OwnerNotFound"] ?? "Owner user not found.");
        }

        var isCharity = await _userManager.IsInRoleAsync(owner, AppRole.Charity);

        // Validate allowed document types based on role
        if (isCharity)
        {
            if (command.VerificationType == UploadDocumentType.StoreFacilityPhoto)
            {
                throw new ArgumentException(_loc["CharityCannotUploadStoreFacilityPhoto"] ?? "Charities cannot upload store facility photos.");
            }
            if (command.VerificationType == UploadDocumentType.TaxIdCertificate)
            {
                throw new ArgumentException(_loc["CharityCannotUploadTaxIdCertificate"] ?? "Charities upload registration and tax id in CommercialRegistration slot.");
            }
        }
        else
        {
            if (command.VerificationType == UploadDocumentType.HealthCertificate)
            {
                throw new ArgumentException(_loc["StoreCannotUploadHealthCertificate"] ?? "Stores cannot upload health certificates.");
            }
        }

        var documentUrl = await _fileStorage.SaveAsync(command.File, $"stores/{store.Id}", cancellationToken);

        // Replace any prior upload of the same type rather than accumulating duplicates.
        var existing = store.Verifications.FirstOrDefault(v => v.VerificationType == command.VerificationType);
        if (existing != null)
        {
            existing.DocumentUrl = documentUrl;
            existing.Status = VerificationStatus.Pending;
            existing.ReviewedAt = null;
            existing.ReviewedBy = null;
        }
        else
        {
            var storeVerification = new StoreVerification
            {
                StoreId = store.Id,
                VerificationType = command.VerificationType,
                DocumentUrl = documentUrl,
                Status = VerificationStatus.Pending,
            };
            _unitOfWork.Repository<StoreVerification>().Add(storeVerification);
            store.Verifications.Add(storeVerification);
        }

        // Determine required document types
        var requiredTypes = isCharity
            ? new[] { UploadDocumentType.CommercialRegistration, UploadDocumentType.HealthCertificate }
            : new[] { UploadDocumentType.CommercialRegistration, UploadDocumentType.TaxIdCertificate, UploadDocumentType.StoreFacilityPhoto };

        if (requiredTypes.All(t => store.Verifications.Any(v => v.VerificationType == t)))
        {
            store.VerificationStatus = VerificationStatus.Pending;
        }

        store.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return store.ToDto();
    }
}
