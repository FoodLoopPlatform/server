using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("charities")]
[Authorize(Roles = AppRole.Charity)]
public class CharitiesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILocalizationService _loc;

    public CharitiesController(IMediator mediator, ILocalizationService loc)
    {
        _mediator = mediator;
        _loc = loc;
    }

    /// <summary>POST /charities/me/documents â€” step 2's document upload for Charities.
    /// Does not require authentication: the charity is identified by the owner's registered email.
    /// Call once per slot with type = AssociationCertificate | CharityBylaws | BoardOfDirectorsList.</summary>
    [HttpPost("me/documents")]
    [AllowAnonymous]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadDocument([FromForm] UploadCharityDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(ApiResponse.Fail(_loc["OwnerEmailRequired"]));
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(ApiResponse.Fail(_loc["FileRequired"]));
        }

        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse.Fail(_loc["InvalidDocumentFormat"]));
        }

        await using var stream = request.File.OpenReadStream();
        var uploadRequest = new FileUploadRequest
        {
            Content = stream,
            FileName = request.File.FileName,
            ContentType = request.File.ContentType,
        };

        var organization = await _mediator.Send(new UploadOrganizationDocumentCommand(request.Email, request.Type, uploadRequest), cancellationToken);
        return Ok(ApiResponse<OrganizationDto>.Ok(organization));
    }
}

public class UploadCharityDocumentRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    /// <summary>Document type: AssociationCertificate | CharityBylaws | BoardOfDirectorsList</summary>
    [Required]
    public UploadDocumentType Type { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}

