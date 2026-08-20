using FoodLoop.Application.Common.Models;
using FoodLoop.Domain.Enums;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

/// <summary>POST /marketplace/products/{id}/report — user reports a listing.</summary>
public record ReportProductCommand(
    Guid ReportedBy, 
    Guid ProductId, 
    ProductReportReason Reason, 
    string? Details, 
    FileUploadRequest? ImageFile = null
) : IRequest<Unit>;


