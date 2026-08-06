using MediatR;
using System;

namespace FoodLoop.Application.Features.Products.Commands;

public record DeleteProductCommand(Guid OwnerId, Guid ProductId) : IRequest;

