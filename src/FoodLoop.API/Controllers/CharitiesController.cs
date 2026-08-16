using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Queries;
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
public class CharitiesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILocalizationService _loc;

    public CharitiesController(IMediator mediator, ILocalizationService loc)
    {
        _mediator = mediator;
        _loc = loc;
    }

    /// <summary>
    /// GET /charities — list all verified charities (public, no auth required).
    /// Used by donation_community_impact screen to populate the charity picker.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCharities(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCharitiesQuery(), cancellationToken);
        return Ok(ApiResponse<System.Collections.Generic.IReadOnlyList<CharityDto>>.Ok(result));
    }

    /// <summary>POST /charities/me/documents — step 2 document upload for Charities.
    /// Does not require authentication: the charity is identified by the owner's registered email.
    /// Call once per slot with type = AssociationCertificate | CharityBylaws | BoardOfDirectorsList.</summary>
    [HttpPost("me/documents")]
    [AllowAnonymous]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadDocument([FromForm] UploadCharityDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse.Fail(_loc["OwnerEmailRequired"]));

        if (request.File == null || request.File.Length == 0)
            return BadRequest(ApiResponse.Fail(_loc["FileRequired"]));

        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext))
            return BadRequest(ApiResponse.Fail(_loc["InvalidDocumentFormat"]));

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

    /// <summary>GET /charities/me/notes — retrieve all notes sent to the current charity coordinator.</summary>
    [HttpGet("me/notes")]
    [Authorize(Roles = AppRole.Charity)]
    public async Task<IActionResult> GetMyNotes(
        [FromServices] ICurrentUserService currentUser,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var query = new GetMyNotesQuery(userId, pageNumber, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminNoteDto>>.Ok(result));
    }

    /// <summary>GET /charities/me/wallet — retrieve current charity's wallet balance and transactions.</summary>
    [HttpGet("me/wallet")]
    [Authorize(Roles = AppRole.Charity)]
    public async Task<IActionResult> GetMyWallet(
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var query = new GetUserWalletQuery(userId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<UserWalletDto>.Ok(result));
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
