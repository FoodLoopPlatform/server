namespace FoodLoop.Application.DTOs.Organizations;

public class AiSettingsDto
{
    public bool AiAutoDiscountEnabled { get; set; }
    public int AiAutoDiscountPercent { get; set; }
    public int AiAutoDiscountDaysBeforeExpiry { get; set; }
    public bool AiAutoPricingEnabled { get; set; }
}
