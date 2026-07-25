using System.Reflection;

namespace FoodLoop.Application;

/// <summary>
/// Marker type with no purpose other than giving other layers (Infrastructure's
/// DI registration, test projects) a stable handle on "the Application assembly"
/// for reflection-based registration — e.g. MediatR's RegisterServicesFromAssembly.
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
