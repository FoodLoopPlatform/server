using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class ModerateProductCommandHandler
    : IRequestHandler<ModerateProductCommand, AdminProductDto>
{
    private readonly ApplicationDbContext _context;

    public ModerateProductCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminProductDto> Handle(
        ModerateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Store)
            .Include(p => p.Category)
            .Include(p => p.AIRecognitionResult)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        switch (request.Action.ToLowerInvariant())
        {
            case "approve":
                product.Status = ProductStatus.Active;
                product.ModerationNote = null;
                break;

            case "reject":
                if (string.IsNullOrWhiteSpace(request.Note))
                {
                    throw new ArgumentException("A reason note is required to reject a product.");
                }
                product.Status = ProductStatus.Rejected;
                product.ModerationNote = request.Note;
                break;

            case "requestchanges":
                if (string.IsNullOrWhiteSpace(request.Note))
                {
                    throw new ArgumentException("Change request instructions are required.");
                }
                product.Status = ProductStatus.ChangeRequested;
                product.ModerationNote = request.Note;
                break;

            default:
                throw new ArgumentException($"Invalid moderation action '{request.Action}'. Action must be 'Approve', 'Reject', or 'RequestChanges'.");
        }

        if (product.AIRecognitionResult != null)
        {
            product.AIRecognitionResult.Reviewed = true;
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new AdminProductDto
        {
            Id = product.Id,
            StoreId = product.StoreId,
            StoreName = product.Store?.Name ?? string.Empty,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            Title = product.Title,
            TitleAr = product.TitleAr,
            OriginalPrice = product.OriginalPrice,
            DiscountedPrice = product.DiscountedPrice,
            QuantityAvailable = product.QuantityAvailable,
            ExpirationDate = product.ExpirationDate,
            Status = product.Status.ToString(),
            AIConfidenceScore = product.AIRecognitionResult?.ConfidenceScore,
            ModerationNote = product.ModerationNote,
            CreatedAt = product.CreatedAt
        };
    }
}

