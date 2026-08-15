using MediatR;
using FoodLoop.Application.DTOs.Organizations;
using System;

namespace FoodLoop.Application.Features.Organizations.Commands;

/// <summary>POST /stores/me/donations — donate surplus inventory to a charity.</summary>
public record DonateSurplusCommand(
    Guid DonorOwnerId,
    Guid RecipientOrganizationId,
    Guid ProductId,
    int Quantity,
    string? Note) : IRequest<DonationDto>;
