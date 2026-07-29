using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Admin;

public class SupportTicketDetailDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<TicketMessageDto> Messages { get; set; } = new();
}
