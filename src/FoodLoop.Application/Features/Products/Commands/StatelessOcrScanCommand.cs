using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Products;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

public record StatelessOcrScanCommand(
    Guid OwnerId,
    FileUploadRequest File) : IRequest<OcrResultDto>;
