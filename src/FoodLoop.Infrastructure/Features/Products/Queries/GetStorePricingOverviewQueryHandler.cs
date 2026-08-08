using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Queries;

public class GetStorePricingOverviewQueryHandler : IRequestHandler<GetStorePricingOverviewQuery, StorePricingOverviewDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetStorePricingOverviewQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<StorePricingOverviewDto> Handle(GetStorePricingOverviewQuery request, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(request.OwnerId, "Organization not found.", cancellationToken);

        var products = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.OrganizationId == organization.Id && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var productList = new List<ProductPricingDto>();
        foreach (var p in products)
        {
            decimal discountPercentage = 0;
            if (p.OriginalPrice > 0)
            {
                discountPercentage = Math.Round((p.OriginalPrice - p.DiscountedPrice) / p.OriginalPrice * 100, 2);
            }

            productList.Add(new ProductPricingDto
            {
                Id = p.Id,
                Title = p.Title,
                OriginalPrice = p.OriginalPrice,
                DiscountedPrice = p.DiscountedPrice,
                DiscountPercentage = discountPercentage,
                QuantityAvailable = p.QuantityAvailable,
                Status = p.Status.ToString(),
                ExpirationDate = p.ExpirationDate
            });
        }

        var activeProducts = productList.Where(p => p.Status == ProductStatus.Active.ToString()).ToList();

        decimal avgDiscount = 0;
        decimal maxDiscount = 0;
        decimal minDiscount = 0;
        decimal totalOriginalValue = 0;
        decimal totalDiscountedValue = 0;

        if (activeProducts.Any())
        {
            avgDiscount = Math.Round(activeProducts.Average(p => p.DiscountPercentage), 2);
            maxDiscount = activeProducts.Max(p => p.DiscountPercentage);
            minDiscount = activeProducts.Min(p => p.DiscountPercentage);
            totalOriginalValue = activeProducts.Sum(p => p.OriginalPrice * p.QuantityAvailable);
            totalDiscountedValue = activeProducts.Sum(p => p.DiscountedPrice * p.QuantityAvailable);
        }

        return new StorePricingOverviewDto
        {
            Summary = new PricingSummaryDto
            {
                TotalActiveProducts = activeProducts.Count,
                AverageDiscountPercentage = avgDiscount,
                MaxDiscountPercentage = maxDiscount,
                MinDiscountPercentage = minDiscount,
                TotalValueAtOriginalPrice = totalOriginalValue,
                TotalValueAtDiscountedPrice = totalDiscountedValue,
                TotalPotentialSavings = totalOriginalValue - totalDiscountedValue
            },
            // Return only active products so the list count matches the summary
            Products = activeProducts
        };
    }
}
