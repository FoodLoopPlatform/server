using FoodLoop.Domain.Common;

namespace FoodLoop.Domain.Entities;

public class UserDeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "Mobile";
    public bool IsActive { get; set; } = true;
}
