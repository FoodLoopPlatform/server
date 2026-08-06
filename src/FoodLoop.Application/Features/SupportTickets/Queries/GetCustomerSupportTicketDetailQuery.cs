using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;

namespace FoodLoop.Application.Features.SupportTickets.Queries;

public record GetCustomerSupportTicketDetailQuery(Guid TicketId, Guid UserId) : IRequest<SupportTicketDetailDto>;
