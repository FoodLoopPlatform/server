using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Stores;
using FoodLoop.Domain.Enums;
using MediatR;

namespace FoodLoop.Application.Features.Stores.Commands;

/// <summary>POST /stores/me/documents — step 2's document upload (called once per document type).
/// The caller is not yet authenticated; the store is looked up via the owner's registered email.</summary>
public record UploadStoreDocumentCommand(string OwnerEmail, UploadDocumentType VerificationType, FileUploadRequest File) : IRequest<StoreDto>;
