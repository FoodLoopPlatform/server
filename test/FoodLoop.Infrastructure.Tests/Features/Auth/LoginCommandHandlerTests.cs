using FluentAssertions;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Auth;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager = MockUserManagerFactory.Create();
    private readonly Mock<IAuthTokenIssuer> _tokenIssuer = new();

    private LoginCommandHandler CreateHandler()
    {
        return new LoginCommandHandler(_userManager.Object, _tokenIssuer.Object);
    }

    [Fact]
    public async Task Handle_should_fail_when_user_not_found()
    {
        // Arrange
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var handler = CreateHandler();
        var request = new LoginRequest { Email = "test@example.com", Password = "Password123!" };
        var command = new LoginCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Handle_should_fail_when_user_is_suspended()
    {
        // Arrange
        var user = new ApplicationUser { Email = "test@example.com", Status = UserStatus.Suspended };
        _userManager.Setup(m => m.FindByEmailAsync("test@example.com")).ReturnsAsync(user);

        var handler = CreateHandler();
        var request = new LoginRequest { Email = "test@example.com", Password = "Password123!" };
        var command = new LoginCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("This account is not active");
    }

    [Fact]
    public async Task Handle_should_fail_when_password_incorrect()
    {
        // Arrange
        var user = new ApplicationUser { Email = "test@example.com", Status = UserStatus.Active };
        _userManager.Setup(m => m.FindByEmailAsync("test@example.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.CheckPasswordAsync(user, "WrongPassword")).ReturnsAsync(false);

        var handler = CreateHandler();
        var request = new LoginRequest { Email = "test@example.com", Password = "WrongPassword" };
        var command = new LoginCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Handle_should_succeed_when_credentials_valid()
    {
        // Arrange
        var user = new ApplicationUser { Email = "test@example.com", Status = UserStatus.Active };
        _userManager.Setup(m => m.FindByEmailAsync("test@example.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.CheckPasswordAsync(user, "Password123!")).ReturnsAsync(true);

        var authResponse = new AuthResponse { AccessToken = "valid-access", RefreshToken = "valid-refresh" };
        _tokenIssuer.Setup(t => t.IssueTokensAsync(user, "127.0.0.1", It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(authResponse);

        var handler = CreateHandler();
        var request = new LoginRequest { Email = "test@example.com", Password = "Password123!" };
        var command = new LoginCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("valid-access");
        result.Data!.RefreshToken.Should().Be("valid-refresh");
    }
}
