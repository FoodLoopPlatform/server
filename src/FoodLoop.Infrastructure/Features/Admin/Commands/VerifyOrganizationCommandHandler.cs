using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class VerifyStoreCommandHandler : IRequestHandler<VerifyOrganizationCommand, AdminOrganizationDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;

    public VerifyStoreCommandHandler(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    public async Task<AdminOrganizationDto> Handle(VerifyOrganizationCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.Organizations.GetByIdWithVerificationsAsync(command.OrganizationId, cancellationToken)
            ?? throw new NotFoundException("Organization", command.OrganizationId);

        var newStatus = command.Request.Action == "Approved"
            ? VerificationStatus.Verified
            : VerificationStatus.Rejected;

        organization.VerificationStatus = newStatus;
        organization.AdminNote = command.Request.Note;
        organization.UpdatedAt = DateTimeOffset.UtcNow;

        var now = DateTimeOffset.UtcNow;
        var pendingDocs = organization.Verifications.Where(v => v.Status == VerificationStatus.Pending).ToList();

        // Stamp each pending document with the review decision.
        foreach (var doc in pendingDocs)
        {
            doc.Status = newStatus;
            doc.ReviewNote = command.Request.Note;
            doc.ReviewedBy = command.AdminId;
            doc.ReviewedAt = now;
        }

        // Activate or deactivate the owner account based on the decision.
        var owner = await _userManager.FindByIdAsync(organization.OwnerId.ToString())
            ?? throw new NotFoundException("User", organization.OwnerId);

        if (newStatus == VerificationStatus.Verified)
        {
            owner.Status = UserStatus.Active;
            await _userManager.UpdateAsync(owner);
        }
        else if (newStatus == VerificationStatus.Rejected)
        {
            // Keep the account as PendingVerification so they can re-submit.
            owner.Status = UserStatus.PendingVerification;
            await _userManager.UpdateAsync(owner);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Log document reviews
        foreach (var doc in pendingDocs)
        {
            await _auditLogService.LogAsync(
                command.AdminId,
                organization.Id,
                "DocumentVerified",
                "Document Reviewed",
                $"{doc.VerificationType} was marked {doc.Status} by admin.",
                null,
                cancellationToken);
        }

        return organization.ToAdminDto(owner);
    }
}


