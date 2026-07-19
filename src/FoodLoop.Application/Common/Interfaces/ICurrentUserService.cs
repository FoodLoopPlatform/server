namespace FoodLoop.Application.Common.Interfaces;

/// <summary>Exposes the identity of the caller, resolved from the JWT claims by Infrastructure.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
}
