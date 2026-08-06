using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Admin;

public class ProductModerationRequest
{
    [MaxLength(500)]
    public string? Note { get; set; }
}
