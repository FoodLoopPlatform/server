using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.Features.Auth.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Auth;
using FoodLoop.Infrastructure.Features.Auth.Commands;
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
    private readonly ApplicationDbContext _dbContext = ApplicationDbContextFactory.Create();

    private readonly Mock<ILocalizationService> _loc = MockLocalizationServiceFactory.Create();
    private readonly Mock<IAuditLogService> _auditLogService = new();

    public void Dispose() => _dbContext.Dispose();

    private RegisterCommandHandler CreateHandler()
    {
        var unitOfWork = new UnitOfWork(_dbContext);
        return new RegisterCommandHandler(
            _userManager.Object,
            unitOfWork,
            _emailService.Object,
            _loc.Object,
            _auditLogService.Object);
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



        var handler = CreateHandler();
        var request = ConsumerRegisterRequest();
        var command = new RegisterCommand(request, "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().BeEmpty();
        result.Data!.RefreshToken.Should().BeEmpty();

        _emailService.Verify(e => e.SendWelcomeEmailAsync("amina@example.com", "Amina Test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_should_register_merchant_and_create_draft_organization_successfully()
    {
        // Arrange
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRole.Merchant))
            .ReturnsAsync(IdentityResult.Success);



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

        // Verify draft organization was inserted into EF InMemory
        var organization = await _dbContext.Organizations.FirstOrDefaultAsync();
        organization.Should().NotBeNull();
        organization!.Name.Should().Be("Amina Bakery");
        organization.VerificationStatus.Should().Be(VerificationStatus.Unverified);
    }
}


