using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Features.Organizations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public DeleteProductCommandHandler(IUnitOfWork unitOfWork, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Organization not found.", cancellationToken);

        var product = await _unitOfWork.Repository<Product>().Query()
            .FirstOrDefaultAsync(l => l.Id == command.ProductId && l.OrganizationId == organization.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        _unitOfWork.Repository<Product>().Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            command.OwnerId,
            organization.Id,
            "ProductDeleted",
            "Product Removed",
            $"Removed product '{product.Title}'.",
            null,
            cancellationToken);
    }
}



