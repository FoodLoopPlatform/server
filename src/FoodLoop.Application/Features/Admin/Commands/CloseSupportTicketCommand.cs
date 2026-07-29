using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

public record CloseSupportTicketCommand(Guid Id) : IRequest;
