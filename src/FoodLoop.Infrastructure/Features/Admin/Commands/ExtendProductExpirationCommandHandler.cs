using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class ExtendProductExpirationCommandHandler : IRequestHandler<ExtendProductExpirationCommand, Result<ExtendProductExpirationResultDto>>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExtendProductExpirationCommandHandler> _logger;

    public ExtendProductExpirationCommandHandler(
        ApplicationDbContext context,
        ILogger<ExtendProductExpirationCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<ExtendProductExpirationResultDto>> Handle(ExtendProductExpirationCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int daysToAdd = request.Days > 0 ? request.Days : 7;

        var query = _context.Products.Where(p => !p.IsDeleted);

        if (request.StoreId.HasValue)
        {
            query = query.Where(p => p.OrganizationId == request.StoreId.Value);
        }

        var products = await query.ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return Result<ExtendProductExpirationResultDto>.Ok(new ExtendProductExpirationResultDto(
                0,
                0,
                today,
                today,
                "No eligible products found to update."
            ));
        }

        int reactivatedCount = 0;

        foreach (var product in products)
        {
            // If the product was already expired in the past, calculate relative to today
            if (product.ExpirationDate < today)
            {
                product.ExpirationDate = today.AddDays(daysToAdd);
            }
            else
            {
                product.ExpirationDate = product.ExpirationDate.AddDays(daysToAdd);
            }

            if (request.ReactivateExpiredProducts && product.Status == ProductStatus.Expired)
            {
                product.Status = ProductStatus.Active;
                reactivatedCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var earliest = products.Min(p => p.ExpirationDate);
        var latest = products.Max(p => p.ExpirationDate);

        var message = $"Successfully extended expiration dates for {products.Count} product(s) by {daysToAdd} days. {reactivatedCount} product(s) reactivated to Active. Expiration window: {earliest:yyyy-MM-dd} to {latest:yyyy-MM-dd}.";

        _logger.LogInformation("Extended expiration dates: Total={Total}, Reactivated={Reactivated}, DaysToAdd={Days}, Scope={Scope}",
            products.Count, reactivatedCount, daysToAdd, request.StoreId.HasValue ? $"Store {request.StoreId}" : "All Stores");

        return Result<ExtendProductExpirationResultDto>.Ok(new ExtendProductExpirationResultDto(
            products.Count,
            reactivatedCount,
            earliest,
            latest,
            message
        ));
    }
}
