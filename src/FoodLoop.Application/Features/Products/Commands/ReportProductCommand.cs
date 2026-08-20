using FoodLoop.Application.Common.Models;
using FoodLoop.Domain.Enums;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

/// <summary>POST /marketplace/products/{id}/report — user reports a listing with mandatory evidence photo.</summary>
public record ReportProductCommand(
    Guid ReportedBy, 
    Guid ProductId, 
    ProductReportReason Reason, 
    string? Details, 
    FileUploadRequest ImageFile
) : IRequest<Unit>;



