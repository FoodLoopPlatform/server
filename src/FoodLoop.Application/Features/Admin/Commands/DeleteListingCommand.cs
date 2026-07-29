using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

public record DeleteListingCommand(Guid Id) : IRequest;
