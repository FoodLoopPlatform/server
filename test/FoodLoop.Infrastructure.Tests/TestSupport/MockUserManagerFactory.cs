using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace FoodLoop.Infrastructure.Tests.TestSupport;

/// <summary>
/// ASP.NET Core Identity's UserManager&lt;TUser&gt; has no interface, so it can't be mocked
/// through a clean abstraction the way most dependencies are. The standard workaround
/// (the same one the ASP.NET Core Identity source itself uses in its own tests): its
/// public members are virtual, so Moq can create a mock of the class directly as long
/// as we satisfy its constructor. Only IUserStore is required; everything else
/// UserManager tolerates as null and falls back to defaults for.
///
/// Returns the Mock&lt;UserManager&lt;ApplicationUser&gt;&gt; itself (not just .Object) so tests
/// can call .Setup(...) / .Verify(...) on methods like FindByEmailAsync, CreateAsync,
/// CheckPasswordAsync, AddToRoleAsync, GetRolesAsync, etc.
/// </summary>
public static class MockUserManagerFactory
{
    public static Mock<UserManager<ApplicationUser>> Create()
    {
        var organization = new Mock<IUserStore<ApplicationUser>>();

        return new Mock<UserManager<ApplicationUser>>(
            organization.Object, null, null, null, null, null, null, null, null);
    }
}

