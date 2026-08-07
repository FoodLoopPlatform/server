using MediatR;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Products;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

/// <summary>POST /stores/me/products/{id}/ocr — submit product image for AI/OCR analysis.</summary>
public record OcrScanCommand(Guid OwnerId, Guid ProductId, FileUploadRequest Image) : IRequest<OcrResultDto>;
