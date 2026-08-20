using FoodLoop.Application.DTOs.Organizations;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Organizations.Queries;

/// <summary>GET /stores/me/disputes/summary — gets store dispute health, active strikes, and repeat product reports.</summary>
public record GetStoreDisputeSummaryQuery(Guid MerchantUserId) : IRequest<StoreDisputeSummaryDto>;
