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

namespace FoodLoop.Infrastructure.Features.Listings;

public class DeleteProductListingCommandHandler : IRequestHandler<DeleteProductListingCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProductListingCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteProductListingCommand command, CancellationToken cancellationToken)
    {
        var store = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Store not found.", cancellationToken);

        var listing = await _unitOfWork.Repository<ProductListing>().Query()
            .FirstOrDefaultAsync(l => l.Id == command.ListingId && l.StoreId == store.Id && !l.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ProductListing", command.ListingId);

        // Soft delete will be applied automatically in DB context SaveChangesAsync,
        // but we can set the audit fields here or mark state as Deleted
        _unitOfWork.Repository<ProductListing>().Remove(listing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
