using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Application.Features.Stores.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Stores;

public class UploadStoreDocumentCommandHandler : IRequestHandler<UploadStoreDocumentCommand, StoreDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;

    public UploadStoreDocumentCommandHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<StoreDto> Handle(UploadStoreDocumentCommand command, CancellationToken cancellationToken)
    {
        if (!DocumentTypes.All.Contains(command.VerificationType))
        {
            throw new ArgumentException(
                $"Unknown document type '{command.VerificationType}'. Expected one of: {string.Join(", ", DocumentTypes.All)}.");
        }

        var store = await _unitOfWork.FindByOwnerEmailOrThrowAsync(command.OwnerEmail, cancellationToken);

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
            store.Verifications.Add(new StoreVerification
            {
                StoreId = store.Id,
                VerificationType = command.VerificationType,
                DocumentUrl = documentUrl,
                Status = VerificationStatus.Pending,
            });
        }

        // Once all three required documents are in, the store moves from Unverified
        // to Pending admin review (matches verification_pending_step_3).
        if (DocumentTypes.All.All(t => store.Verifications.Any(v => v.VerificationType == t)))
        {
            store.VerificationStatus = VerificationStatus.Pending;
        }

        store.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return store.ToDto();
    }
}
