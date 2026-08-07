using MediatR;
using FoodLoop.Application.DTOs.Organizations;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Organizations.Queries;

/// <summary>GET /charities — list all verified charities for donation selection.</summary>
public record GetCharitiesQuery : IRequest<IReadOnlyList<CharityDto>>;
