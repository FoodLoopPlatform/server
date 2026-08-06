using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.SupportTickets.Queries;

public record GetCustomerSupportTicketsQuery(Guid UserId) : IRequest<IReadOnlyList<SupportTicketDto>>;
