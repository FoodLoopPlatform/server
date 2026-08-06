using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Application.Features.Listings.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Listings.Commands;

public class BulkUploadProductsCommandHandler : IRequestHandler<BulkUploadProductsCommand, IReadOnlyList<ProductDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public BulkUploadProductsCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ProductDto>> Handle(BulkUploadProductsCommand command, CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.FindByOwnerOrThrowAsync(command.OwnerId, "Organization not found.", cancellationToken);
        if (organization.VerificationStatus != VerificationStatus.Verified)
        {
            throw new ArgumentException("Your organization must be verified by an admin before you can manage products.");
        }

        var categories = await _unitOfWork.Repository<Category>().Query()
            .Where(c => !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var resultProducts = new List<Product>();

        using var reader = new StreamReader(command.File.Content, Encoding.UTF8);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new ArgumentException("The uploaded CSV file is empty.");
        }

        var headers = ParseCsvLine(headerLine).Select(h => h.Trim().ToLower()).ToList();

        // Required headers
        var requiredHeaders = new[] { "title", "originalprice", "discountedprice", "quantityavailable", "expirationdate", "categoryname" };
        foreach (var req in requiredHeaders)
        {
            if (!headers.Contains(req))
            {
                throw new ArgumentException($"Missing required CSV header: '{req}'");
            }
        }

        var rowNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            rowNum++;
            var values = ParseCsvLine(line);
            if (values.Length < requiredHeaders.Length)
            {
                throw new ArgumentException($"Row {rowNum} has insufficient fields. Expected at least {requiredHeaders.Length} columns.");
            }

            var title = GetCsvValue(headers, values, "title");
            var titleAr = GetCsvValue(headers, values, "titlear");
            var desc = GetCsvValue(headers, values, "description");
            var descAr = GetCsvValue(headers, values, "descriptionar");
            var origPriceStr = GetCsvValue(headers, values, "originalprice");
            var discPriceStr = GetCsvValue(headers, values, "discountedprice");
            var qtyStr = GetCsvValue(headers, values, "quantityavailable");
            var expDateStr = GetCsvValue(headers, values, "expirationdate");
            var categoryName = GetCsvValue(headers, values, "categoryname");

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException($"Row {rowNum}: Title is required.");
            }

            if (!decimal.TryParse(origPriceStr, out var originalPrice) || originalPrice < 0)
            {
                throw new ArgumentException($"Row {rowNum}: Invalid or negative OriginalPrice '{origPriceStr}'.");
            }

            if (!decimal.TryParse(discPriceStr, out var discountedPrice) || discountedPrice < 0)
            {
                throw new ArgumentException($"Row {rowNum}: Invalid or negative DiscountedPrice '{discPriceStr}'.");
            }

            if (discountedPrice > originalPrice)
            {
                throw new ArgumentException($"Row {rowNum}: DiscountedPrice cannot be greater than OriginalPrice.");
            }

            if (!int.TryParse(qtyStr, out var quantity) || quantity < 0)
            {
                throw new ArgumentException($"Row {rowNum}: Invalid or negative QuantityAvailable '{qtyStr}'.");
            }

            if (!DateOnly.TryParse(expDateStr, out var expirationDate))
            {
                throw new ArgumentException($"Row {rowNum}: Invalid ExpirationDate format '{expDateStr}'. Use YYYY-MM-DD.");
            }

            var category = categories.FirstOrDefault(c =>
                c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase) ||
                (c.NameAr != null && c.NameAr.Equals(categoryName, StringComparison.OrdinalIgnoreCase)))
                ?? throw new ArgumentException($"Row {rowNum}: Category '{categoryName}' not found.");

            var product = new Product
            {
                OrganizationId = organization.Id,
                CategoryId = category.Id,
                Category = category,
                Title = title,
                TitleAr = titleAr,
                Description = desc,
                DescriptionAr = descAr,
                OriginalPrice = originalPrice,
                DiscountedPrice = discountedPrice,
                QuantityAvailable = quantity,
                ExpirationDate = expirationDate,
                Status = ProductStatus.Active
            };

            _unitOfWork.Repository<Product>().Add(product);
            resultProducts.Add(product);
        }

        if (!resultProducts.Any())
        {
            throw new ArgumentException("No product rows found in the CSV file.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return resultProducts.Select(l => l.ToDto()).ToList();
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var currentToken = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentToken.ToString().Trim(' ', '"'));
                currentToken.Clear();
            }
            else
            {
                currentToken.Append(c);
            }
        }
        result.Add(currentToken.ToString().Trim(' ', '"'));
        return result.ToArray();
    }

    private static string GetCsvValue(List<string> headers, string[] values, string headerName)
    {
        var idx = headers.IndexOf(headerName);
        if (idx < 0 || idx >= values.Length) return string.Empty;
        return values[idx];
    }
}


