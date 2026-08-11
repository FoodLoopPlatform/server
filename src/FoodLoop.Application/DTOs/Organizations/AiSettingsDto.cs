namespace FoodLoop.Application.DTOs.Organizations;

public class AiSettingsDto
{
    /// <summary>"manual" | "assisted" | "autonomous"</summary>
    public string AutomationMode => AiAutoPricingEnabled ? "autonomous" : (AiAutoDiscountEnabled ? "assisted" : "manual");

    public bool AiAutoDiscountEnabled { get; set; }
    public int AiAutoDiscountPercent { get; set; }
    public int AiAutoDiscountDaysBeforeExpiry { get; set; }
    public bool AiAutoPricingEnabled { get; set; }
}
