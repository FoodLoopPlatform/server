using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Domain.Entities;

public class SupportTicket : BaseEntity
{
    public Guid UserId { get; set; }

    public string Category { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}
