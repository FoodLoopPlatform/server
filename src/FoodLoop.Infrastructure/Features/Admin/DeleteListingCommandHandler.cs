using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class DeleteListingCommandHandler : IRequestHandler<DeleteListingCommand>
{
    private readonly ApplicationDbContext _context;

    public DeleteListingCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.ProductListings
            .FirstOrDefaultAsync(l => l.Id == request.Id && !l.IsDeleted, cancellationToken);

        if (listing == null)
        {
            throw new NotFoundException("Listing", request.Id);
        }

        listing.IsDeleted = true;
        listing.DeletedAt = System.DateTimeOffset.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);
    }
}
