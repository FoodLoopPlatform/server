using FoodLoop.Domain.Common;

namespace FoodLoop.Domain.Entities;

public class Category : BaseEntity, ISoftDelete
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Icon { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
}
