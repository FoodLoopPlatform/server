namespace FoodLoop.Domain.Enums;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Preparing = 2,
    ReadyForPickup = 3,
    Completed = 4,
    Cancelled = 5
}
