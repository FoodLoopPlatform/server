using MediatR;
using FoodLoop.Application.DTOs.Products;
using System;

namespace FoodLoop.Application.Features.Products.Queries;

/// <summary>GET /stores/me/products/{id}/ocr-result — poll the latest OCR result for a product.</summary>
public record GetOcrResultQuery(Guid OwnerId, Guid ProductId) : IRequest<OcrResultDto>;
