using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetAnalyticsSummaryQuery : IRequest<AnalyticsSummaryDto>;
