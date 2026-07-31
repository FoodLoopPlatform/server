using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

public record AdminDeleteProductCommand(Guid Id) : IRequest;
