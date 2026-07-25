using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Stores;
using MediatR;

namespace FoodLoop.Application.Features.Stores.Commands;

/// <summary>POST /stores/me/documents — step 2's document upload (called once per document type).</summary>
public record UploadStoreDocumentCommand(Guid OwnerId, string VerificationType, FileUploadRequest File) : IRequest<StoreDto>;
