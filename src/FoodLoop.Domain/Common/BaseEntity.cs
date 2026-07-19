namespace FoodLoop.Domain.Common;

/// <summary>
/// Base class for all domain entities providing a strongly typed primary key
/// and standard audit fields (createdAt/updatedAt/createdBy/updatedBy).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Implemented by entities that support soft-deletion instead of hard removal,
/// per the "Soft Delete Strategy" in the Database Design document.
/// </summary>
public interface ISoftDelete
{
    DateTimeOffset? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
    bool IsDeleted { get; set; }
}
