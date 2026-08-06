using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Users;

public class CreateSupportTicketRequest
{
    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
}
