using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Reviews;
using FoodLoop.Application.Features.Reviews.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Reviews.Commands;

public class SubmitReviewCommandHandler : IRequestHandler<SubmitReviewCommand, Result<ReviewDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLog;

    public SubmitReviewCommandHandler(ApplicationDbContext db, IAuditLogService auditLog)
    {
        _db = db;
        _auditLog = auditLog;
    }

    public async Task<Result<ReviewDto>> Handle(SubmitReviewCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        if (order.UserId != request.UserId)
        {
            return Result<ReviewDto>.Fail("You can only review your own orders.");
        }

        if (order.OrderStatus != OrderStatus.Completed)
        {
            return Result<ReviewDto>.Fail("You can only review completed orders.");
        }

        var alreadyReviewed = await _db.Reviews.AnyAsync(r => r.OrderId == request.OrderId, cancellationToken);
        if (alreadyReviewed)
        {
            return Result<ReviewDto>.Fail("This order has already been reviewed.");
        }

        var firstItem = order.Items.FirstOrDefault();
        if (firstItem == null || firstItem.Product == null)
        {
            return Result<ReviewDto>.Fail("Cannot review an order with no items.");
        }

        var orgId = firstItem.Product.OrganizationId;
        var org = await _db.Organizations.FindAsync(new object[] { orgId }, cancellationToken);

        var review = new Review
        {
            OrderId = request.OrderId,
            UserId = request.UserId,
            OrganizationId = orgId,
            Rating = request.Rating,
            Comment = request.Comment
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        // Audit Logging
        await _auditLog.LogAsync(
            request.UserId,
            orgId,
            "ReviewSubmitted",
            "Review Submitted",
            $"Submitted {request.Rating}-star review for order {request.OrderId}.",
            null,
            cancellationToken);

        var user = await _db.Users.FindAsync(new object[] { request.UserId }, cancellationToken);

        return Result<ReviewDto>.Ok(new ReviewDto
        {
            Id = review.Id,
            OrderId = review.OrderId,
            UserId = review.UserId,
            UserFullName = user?.FullName ?? string.Empty,
            OrganizationId = review.OrganizationId,
            OrganizationName = org?.Name ?? string.Empty,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        });
    }
}
