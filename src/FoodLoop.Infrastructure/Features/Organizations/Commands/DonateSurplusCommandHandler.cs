using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Commands;

public class DonateSurplusCommandHandler : IRequestHandler<DonateSurplusCommand, DonationDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public DonateSurplusCommandHandler(IUnitOfWork unitOfWork, ApplicationDbContext db, IAuditLogService audit)
    {
        _unitOfWork = unitOfWork;
        _db = db;
        _audit = audit;
    }

    public async Task<DonationDto> Handle(DonateSurplusCommand request, CancellationToken cancellationToken)
    {
        var donor = await _unitOfWork.FindByOwnerOrThrowAsync(request.DonorOwnerId, "Donor organization not found.", cancellationToken);

        var recipient = await _db.Organizations.FirstOrDefaultAsync(
            o => o.Id == request.RecipientOrganizationId && !o.IsDeleted && o.VerificationStatus == VerificationStatus.Verified,
            cancellationToken)
            ?? throw new NotFoundException("Charity", request.RecipientOrganizationId);

        var product = await _unitOfWork.Repository<Product>().Query()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.OrganizationId == donor.Id && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (request.Quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.");
        if (request.Quantity > product.QuantityAvailable)
            throw new ArgumentException($"Cannot donate {request.Quantity} units; only {product.QuantityAvailable} available.");

        // Deduct from inventory
        product.QuantityAvailable -= request.Quantity;
        _unitOfWork.Repository<Product>().Update(product);

        var donation = new Donation
        {
            DonorOrganizationId = donor.Id,
            RecipientOrganizationId = recipient.Id,
            ProductId = product.Id,
            Quantity = request.Quantity,
            Note = request.Note,
            Status = "Pending"
        };
        _unitOfWork.Repository<Donation>().Add(donation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.DonorOwnerId, donor.Id, "DonationMade",
            "Donation Made", $"Donated {request.Quantity}x '{product.Title}' to {recipient.Name}.",
            null, cancellationToken);

        return new DonationDto
        {
            Id = donation.Id,
            DonorOrganizationId = donor.Id,
            DonorName = donor.Name,
            RecipientOrganizationId = recipient.Id,
            RecipientName = recipient.Name,
            ProductId = product.Id,
            ProductTitle = product.Title,
            Quantity = donation.Quantity,
            Note = donation.Note,
            Status = donation.Status,
            CreatedAt = donation.CreatedAt
        };
    }
}
