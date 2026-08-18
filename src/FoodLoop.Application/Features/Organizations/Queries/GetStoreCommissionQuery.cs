using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Organizations.Queries;

public record GetStoreCommissionQuery(Guid OwnerId) : IRequest<StoreCommissionDto>;
