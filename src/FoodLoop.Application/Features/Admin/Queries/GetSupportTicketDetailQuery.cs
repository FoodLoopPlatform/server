using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetSupportTicketDetailQuery(Guid Id) : IRequest<SupportTicketDetailDto>;
