using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Domain.Enums;
using MediatR;

namespace FoodLoop.Application.Features.Organizations.Commands;

/// <summary>POST /organizations/me/documents â€” step 2's document upload (called once per document type).
/// The caller is not yet authenticated; the organization is looked up via the owner's registered email.</summary>
public record UploadOrganizationDocumentCommand(string OwnerEmail, UploadDocumentType VerificationType, FileUploadRequest File) : IRequest<OrganizationDto>;

