using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.Features.Products.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Organizations;
using FoodLoop.Infrastructure.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Products.Commands;

public class BulkUploadProductsCommandHandler : IRequestHandler<BulkUploadProductsCommand, IReadOnlyList<ProductDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly IRealTimeNotificationService _notificationService;
    private readonly ILogger<BulkUploadProductsCommandHandler> _logger;

    public BulkUploadProductsCommandHandler(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        IRealTimeNotificationService notificationService,
        ILogger<BulkUploadProductsCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _logger = logger;
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
            var desc = GetCsvValue(headers, values, "description");
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

            var verificationStateStr = GetCsvValue(headers, values, "expiryverificationstate");
            var verificationState = ExpiryVerificationState.Manual;
            if (!string.IsNullOrWhiteSpace(verificationStateStr) && Enum.TryParse<ExpiryVerificationState>(verificationStateStr, true, out var parsedState))
            {
                verificationState = parsedState;
            }

            var product = new Product
            {
                OrganizationId = organization.Id,
                CategoryId = category.Id,
                Category = category,
                Title = title,
                Description = desc,
                OriginalPrice = originalPrice,
                DiscountedPrice = discountedPrice,
                QuantityAvailable = quantity,
                ExpirationDate = expirationDate,
                ExpiryVerificationState = verificationState,
                Status = ProductStatus.PendingModeration
            };

            _unitOfWork.Repository<Product>().Add(product);
            resultProducts.Add(product);

            var history = new PriceHistory
            {
                ProductId = product.Id,
                OldOriginalPrice = 0,
                OldDiscountedPrice = 0,
                NewOriginalPrice = product.OriginalPrice,
                NewDiscountedPrice = product.DiscountedPrice,
                ChangeReason = "CSV import",
                ChangedBy = command.OwnerId
            };
            _unitOfWork.Repository<PriceHistory>().Add(history);
        }

        if (!resultProducts.Any())
        {
            throw new ArgumentException("No product rows found in the CSV file.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var product in resultProducts)
        {
            try
            {
                await _notificationService.SendNotificationToRoleAsync(
                    "Admin",
                    "NotifProductModerationTitle",
                    "NotifProductModerationBodyCsv",
                    "ProductUploaded",
                    new object[] { product.Title, organization.Name },
                    "Product",
                    product.Id,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send ProductUploaded notification for bulk-imported product {ProductId}.", product.Id);
            }
        }

        await _auditLogService.LogAsync(
            command.OwnerId,
            organization.Id,
            "ProductsBulkImported",
            "Bulk Products Uploaded",
            $"Merchant imported {resultProducts.Count} products via CSV file.",
            null,
            cancellationToken);

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




