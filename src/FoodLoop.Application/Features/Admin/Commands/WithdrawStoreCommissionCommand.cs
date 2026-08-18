using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

public record WithdrawStoreCommissionCommand(
    Guid StoreId,
    decimal Amount
) : IRequest<StoreCommissionDto>;
