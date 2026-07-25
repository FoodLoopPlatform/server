using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Services;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager = MockUserManagerFactory.Create();
    private readonly Mock<IJwtTokenService> _tokenService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly ApplicationDbContext _dbContext = ApplicationDbContextFactory.Create();

    // xUnit creates a new instance of the test class for every [Fact]/[Theory], so this
    // runs once per test - each test gets a clean InMemory database and disposes it after.
    public void Dispose() => _dbContext.Dispose();

    private AuthService CreateService() => new(
        _userManager.Object,
        _dbContext,
        _tokenService.Object,
        _emailService.Object,
        NullLogger<AuthService>.Instance);

    private void SetUpTokenIssuance()
    {
        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns("fake-access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString());
        _tokenService.Setup(t => t.GetAccessTokenExpiry()).Returns(DateTimeOffset.UtcNow.AddMinutes(15));
        _tokenService.Setup(t => t.GetRefreshTokenExpiry()).Returns(DateTimeOffset.UtcNow.AddDays(30));
    }

    private static RegisterRequest ConsumerRegisterRequest() => new()
    {
        Name = "Amina Test",
        Email = "amina@example.com",
        Password = "Password123!",
        AccountType = AccountType.User,
    };

    // ---------- RegisterAsync ----------

    [Fact]
    public async Task RegisterAsync_should_fail_when_business_account_has_no_business_name()
    {
        var service = CreateService();
        var request = ConsumerRegisterRequest();
        request.AccountType = AccountType.StoreOwner;
        request.BusinessName = null;

        var result = await service.RegisterAsync(request, ipAddress: null);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Business name is required");
        _userManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_should_fail_when_email_is_already_registered()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { Email = "amina@example.com" });

        var service = CreateService();
        var result = await service.RegisterAsync(ConsumerRegisterRequest(), ipAddress: null);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Email is already registered.");
    }

    [Fact]
    public async Task RegisterAsync_should_create_a_consumer_and_issue_tokens_on_success()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRole.Consumer)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { AppRole.Consumer });
        SetUpTokenIssuance();

        var service = CreateService();
        var result = await service.RegisterAsync(ConsumerRegisterRequest(), ipAddress: "127.0.0.1");

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("fake-access-token");
        result.Data.User.Roles.Should().Contain(AppRole.Consumer);
        _dbContext.Stores.Should().BeEmpty("a plain consumer signup should not create a draft store");
        _emailService.Verify(e => e.SendWelcomeEmailAsync("amina@example.com", "Amina Test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_should_create_a_draft_store_for_a_store_owner_account()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRole.Merchant)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { AppRole.Merchant });
        SetUpTokenIssuance();

        var request = ConsumerRegisterRequest();
        request.AccountType = AccountType.StoreOwner;
        request.BusinessName = "Nile Grocer";

        var service = CreateService();
        var result = await service.RegisterAsync(request, ipAddress: null);

        result.Success.Should().BeTrue();
        var store = _dbContext.Stores.Should().ContainSingle().Subject;
        store.Name.Should().Be("Nile Grocer");
        store.StoreType.Should().Be(StoreType.Standard);
        store.VerificationStatus.Should().Be(VerificationStatus.Unverified);
    }

    // ---------- LoginAsync ----------

    [Fact]
    public async Task LoginAsync_should_fail_with_a_generic_message_when_user_does_not_exist()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest { Email = "nobody@example.com", Password = "x" }, null);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_should_fail_when_the_account_is_suspended()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { Email = "amina@example.com", Status = UserStatus.Suspended });

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest { Email = "amina@example.com", Password = "x" }, null);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not active");
        _userManager.Verify(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_should_fail_when_the_password_is_wrong()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { Email = "amina@example.com", Status = UserStatus.Active });
        _userManager.Setup(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(false);

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest { Email = "amina@example.com", Password = "wrong" }, null);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_should_issue_tokens_on_success()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { Email = "amina@example.com", Status = UserStatus.Active });
        _userManager.Setup(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(true);
        _userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { AppRole.Consumer });
        SetUpTokenIssuance();

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest { Email = "amina@example.com", Password = "correct" }, "127.0.0.1");

        result.Success.Should().BeTrue();
        result.Data!.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    // ---------- RefreshTokenAsync ----------

    [Fact]
    public async Task RefreshTokenAsync_should_fail_when_the_token_does_not_exist()
    {
        var service = CreateService();
        var result = await service.RefreshTokenAsync("does-not-exist", null);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid refresh token.");
    }

    [Fact]
    public async Task RefreshTokenAsync_should_revoke_every_sibling_session_on_reuse_of_a_revoked_token()
    {
        var userId = Guid.NewGuid();
        var reusedToken = new RefreshToken { UserId = userId, Token = "reused", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var stillActiveSibling = new RefreshToken { UserId = userId, Token = "still-active", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };
        _dbContext.RefreshTokens.AddRange(reusedToken, stillActiveSibling);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var result = await service.RefreshTokenAsync("reused", "10.0.0.1");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("log in again");
        (await _dbContext.RefreshTokens.FindAsync(stillActiveSibling.Id))!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_should_rotate_the_token_and_issue_a_new_pair_when_valid()
    {
        var userId = Guid.NewGuid();
        var original = new RefreshToken { UserId = userId, Token = "valid-token", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };
        _dbContext.RefreshTokens.Add(original);
        await _dbContext.SaveChangesAsync();

        _userManager.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(new ApplicationUser { Id = userId, Email = "amina@example.com", Status = UserStatus.Active });
        _userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { AppRole.Consumer });
        SetUpTokenIssuance();

        var service = CreateService();
        var result = await service.RefreshTokenAsync("valid-token", "10.0.0.1");

        result.Success.Should().BeTrue();
        result.Data!.RefreshToken.Should().NotBe("valid-token");
        original.RevokedAt.Should().NotBeNull();
        original.ReplacedByToken.Should().Be(result.Data.RefreshToken);
    }

    // ---------- ForgotPasswordAsync ----------

    [Fact]
    public async Task ForgotPasswordAsync_should_return_success_without_sending_email_for_an_unknown_address()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var service = CreateService();
        var result = await service.ForgotPasswordAsync("nobody@example.com");

        result.Success.Should().BeTrue("the API must not reveal whether an email is registered");
        _emailService.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_should_send_a_reset_email_for_a_known_address()
    {
        var user = new ApplicationUser { Email = "amina@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("amina@example.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");

        var service = CreateService();
        var result = await service.ForgotPasswordAsync("amina@example.com");

        result.Success.Should().BeTrue();
        _emailService.Verify(e => e.SendPasswordResetEmailAsync("amina@example.com", "reset-token", It.IsAny<CancellationToken>()), Times.Once);
    }
}
