using FoodLoop.Domain.Common;

namespace FoodLoop.Domain.Entities;

public class TicketMessage : BaseEntity
{
    public Guid TicketId { get; set; }
    public SupportTicket? Ticket { get; set; }

    public Guid SenderId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Attachment { get; set; }
}
