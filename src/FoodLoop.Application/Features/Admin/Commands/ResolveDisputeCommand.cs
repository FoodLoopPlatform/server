using MediatR;
using FoodLoop.Application.DTOs.Admin;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

/// <summary>PATCH /admin/disputes/{id}/resolve — mark a product report as resolved.</summary>
public record ResolveDisputeCommand(Guid DisputeId, Guid AdminId, string AdminNote) : IRequest<DisputeDto>;
