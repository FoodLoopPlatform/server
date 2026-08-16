using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Organizations.Commands;

public record ResolveStoreDisputeCommand(
    Guid DisputeId,
    Guid MerchantUserId,
    string MerchantNote,
    decimal RefundAmount) : IRequest<DisputeDto>;
