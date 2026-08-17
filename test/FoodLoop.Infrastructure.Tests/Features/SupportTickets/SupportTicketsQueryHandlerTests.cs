using FluentAssertions;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.SupportTickets.Commands;
using FoodLoop.Application.Features.SupportTickets.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.SupportTickets.Commands;
using FoodLoop.Infrastructure.Features.SupportTickets.Queries;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.SupportTickets;

public class SupportTicketsQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();

    public SupportTicketsQueryHandlerTests()
    {
        // Seed user and ticket
        var user = new Identity.ApplicationUser
        {
            Id = _userId,
            UserName = "user@example.com",
            Email = "user@example.com",
            FullName = "Ticket Submitter"
        };
        _db.Users.Add(user);

        var ticket = new SupportTicket
        {
            Id = _ticketId,
            UserId = _userId,
            Category = "Payment",
            Priority = TicketPriority.Low,
            Status = TicketStatus.Open
        };

        var initialMessage = new TicketMessage
        {
            TicketId = _ticketId,
            SenderId = _userId,
            Message = "Payment issue details"
        };
        ticket.Messages.Add(initialMessage);

        _db.SupportTickets.Add(ticket);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetMyTickets_should_return_user_ticket_history()
    {
        // Arrange
        var handler = new GetCustomerSupportTicketsQueryHandler(_db);
        var query = new GetCustomerSupportTicketsQuery(_userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Category.Should().Be("Payment");
        result.First().Status.Should().Be("Open");
    }

    [Fact]
    public async Task GetTicketDetail_should_load_conversation_messages()
    {
        // Arrange
        var handler = new GetCustomerSupportTicketDetailQueryHandler(_db);
        var query = new GetCustomerSupportTicketDetailQuery(_ticketId, _userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Category.Should().Be("Payment");
        result.Messages.Should().HaveCount(1);
        result.Messages.First().Message.Should().Be("Payment issue details");
    }

    [Fact]
    public async Task CustomerReply_should_add_message_to_conversation()
    {
        // Arrange
        _db.ChangeTracker.Clear();
        var handler = new CustomerReplyToSupportTicketCommandHandler(_db);
        var command = new CustomerReplyToSupportTicketCommand(_userId, _ticketId, "Additional info");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Message.Should().Be("Additional info");

        _db.ChangeTracker.Clear();
        var ticket = _db.SupportTickets.Include(t => t.Messages).FirstOrDefault(t => t.Id == _ticketId);
        ticket!.Messages.Should().HaveCount(2);
        ticket.Messages.Last().Message.Should().Be("Additional info");
    }
}
