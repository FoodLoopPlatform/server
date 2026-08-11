using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Organizations;

public class AiSettingsDto
{
    public AutomationMode AutomationMode => AiAutoPricingEnabled 
        ? AutomationMode.Autonomous 
        : (AiAutoDiscountEnabled ? AutomationMode.Assisted : AutomationMode.Manual);

    public bool AiAutoDiscountEnabled { get; set; }
    public int AiAutoDiscountPercent { get; set; }
    public int AiAutoDiscountDaysBeforeExpiry { get; set; }
    public bool AiAutoPricingEnabled { get; set; }
}
