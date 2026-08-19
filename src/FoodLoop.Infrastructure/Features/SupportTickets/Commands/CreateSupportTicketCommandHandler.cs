using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.SupportTickets.Commands;

public class CreateSupportTicketCommandHandler
    : IRequestHandler<CreateSupportTicketCommand, SupportTicketDto>
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLogService;
    private readonly IRealTimeNotificationService _notificationService;

    public CreateSupportTicketCommandHandler(
        ApplicationDbContext db,
        IAuditLogService auditLogService,
        IRealTimeNotificationService notificationService)
    {
        _db = db;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
    }

    public async Task<SupportTicketDto> Handle(CreateSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var ticket = new SupportTicket
        {
            UserId = request.UserId,
            Category = request.Category,
            Priority = request.Priority,
            Status = TicketStatus.Open
        };

        var initialMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = request.UserId,
            Message = request.Message
        };

        ticket.Messages.Add(initialMessage);

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.UserId && !o.IsDeleted, cancellationToken);
        await _auditLogService.LogAsync(
            request.UserId,
            org?.Id,
            "SupportTicket",
            "Support Ticket Opened",
            $"Ticket: {ticket.Category} — {ticket.Status}.",
            null,
            cancellationToken);

        if (_notificationService != null)
        {
            await _notificationService.SendNotificationToRoleAsync(
                "Admin",
                "NotifSupportTicketCreatedTitle",
                "NotifSupportTicketCreatedBody",
                "SupportTicketCreated",
                new object[] { ticket.Category, user.FullName },
                "SupportTicket",
                ticket.Id,
                cancellationToken);
        }

        return new SupportTicketDto
        {
            Id = ticket.Id,
            UserId = ticket.UserId,
            UserEmail = user.Email ?? string.Empty,
            UserFullName = user.FullName,
            Category = ticket.Category,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        };
    }
}

