using MediatR;
using FoodLoop.Application.DTOs.Admin;
using System;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/disputes/{id} — get detailed dispute record by ID.</summary>
public record GetDisputeByIdQuery(Guid Id) : IRequest<DisputeDto>;
