using System;

namespace FoodLoop.Application.DTOs.Admin;

public class TicketMessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Attachment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
