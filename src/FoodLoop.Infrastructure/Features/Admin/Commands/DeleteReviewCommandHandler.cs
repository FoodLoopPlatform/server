using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand>
{
    private readonly ApplicationDbContext _context;
    private readonly FoodLoop.Application.Common.Interfaces.IAuditLogService _auditLogService;

    public DeleteReviewCommandHandler(ApplicationDbContext context, FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
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

        await _auditLogService.LogAsync(
            review.UserId,
            review.OrganizationId,
            "ReviewModerated",
            "Customer Review Removed",
            $"Administrator removed review (Rating: {review.Rating}/5) for organization '{review.OrganizationId}'.",
            null,
            cancellationToken);
    }
}

