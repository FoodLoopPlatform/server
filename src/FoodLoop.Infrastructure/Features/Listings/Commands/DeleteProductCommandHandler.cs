using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.Listings.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Features.Stores;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings.Commands;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProductCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Store not found.", cancellationToken);

        var product = await _unitOfWork.Repository<Product>().Query()
            .FirstOrDefaultAsync(l => l.Id == command.ProductId && l.StoreId == store.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        _unitOfWork.Repository<Product>().Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

