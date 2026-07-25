using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Auth;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Auth;

public class RegisterCommandHandlerTests : IDisposable
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager = MockUserManagerFactory.Create();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IAuthTokenIssuer> _tokenIssuer = new();
    private readonly ApplicationDbContext _dbContext = ApplicationDbContextFactory.Create();

    public void Dispose() => _dbContext.Dispose();

    private RegisterCommandHandler CreateHandler()
    {
        var unitOfWork = new UnitOfWork(_dbContext);
        return new RegisterCommandHandler(
            _userManager.Object,
            unitOfWork,
            _emailService.Object,
            _tokenIssuer.Object);
    }

    private static RegisterRequest ConsumerRegisterRequest() => new()
    {
        Name = "Amina Test",
        Email = "amina@example.com",
        Password = "Password123!",
        Role = AppRole.Customer,
    };

    [Fact]
    public async Task Handle_should_fail_when_business_account_has_no_business_name()
    {
        // Arrange
        var handler = CreateHandler();
        var request = ConsumerRegisterRequest();
        request.Role = AppRole.Merchant;
        request.BusinessName = null;

        var command = new RegisterCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Business name is required");
    }

    [Fact]
    public async Task Handle_should_fail_when_email_already_registered()
    {
        // Arrange
        var existingUser = new ApplicationUser { Email = "amina@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("amina@example.com")).ReturnsAsync(existingUser);

        var handler = CreateHandler();
        var request = ConsumerRegisterRequest();
        var command = new RegisterCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already registered");
    }

    [Fact]
    public async Task Handle_should_register_consumer_successfully()
    {
        // Arrange
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRole.Customer))
            .ReturnsAsync(IdentityResult.Success);

        _tokenIssuer.Setup(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(new AuthResponse { AccessToken = "access", RefreshToken = "refresh" });

        var handler = CreateHandler();
        var request = ConsumerRegisterRequest();
        var command = new RegisterCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access");

        _emailService.Verify(e => e.SendWelcomeEmailAsync("amina@example.com", "Amina Test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_should_register_merchant_and_create_draft_store_successfully()
    {
        // Arrange
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRole.Merchant))
            .ReturnsAsync(IdentityResult.Success);

        _tokenIssuer.Setup(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(new AuthResponse { AccessToken = "access", RefreshToken = "refresh" });

        var handler = CreateHandler();
        var request = ConsumerRegisterRequest();
        request.Role = AppRole.Merchant;
        request.BusinessName = "Amina Bakery";
        request.BusinessCategory = BusinessCategory.Bakery;

        var command = new RegisterCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify draft store was inserted into EF InMemory
        var store = await _dbContext.Stores.FirstOrDefaultAsync();
        store.Should().NotBeNull();
        store!.Name.Should().Be("Amina Bakery");
        store.VerificationStatus.Should().Be(VerificationStatus.Unverified);
        store.StoreType.Should().Be(StoreType.Standard);
    }
}
