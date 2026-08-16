using FoodLoop.Application.DTOs.Users;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Users.Queries;

public record GetUserWalletQuery(Guid UserId) : IRequest<UserWalletDto>;
