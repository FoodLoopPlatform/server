using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand>
{
    private readonly ApplicationDbContext _context;

    public DeleteReviewCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (review == null)
        {
            throw new NotFoundException("Review", request.Id);
        }

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
