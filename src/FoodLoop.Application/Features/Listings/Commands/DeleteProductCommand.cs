using MediatR;
using System;

namespace FoodLoop.Application.Features.Listings.Commands;

public record DeleteProductCommand(Guid OwnerId, Guid ProductId) : IRequest;
