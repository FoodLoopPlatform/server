using FluentAssertions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Auth;
using FoodLoop.Application.DTOs.Users;
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
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.Auth;

public class AuthTokenLifecycleEdgeCaseTests : IDisposable
{
    private readonly ApplicationDbContext _db = ApplicationDbContextFactory.Create();
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager = MockUserManagerFactory.Create();
    private readonly Mock<IJwtTokenService> _mockTokenService = new();
    private readonly Mock<IAuthTokenIssuer> _mockTokenIssuer = new();
    private readonly Mock<ILocalizationService> _mockLoc = MockLocalizationServiceFactory.Create();
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _mockLogger = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly ApplicationUser _testUser;

    public AuthTokenLifecycleEdgeCaseTests()
    {
        _unitOfWork = new UnitOfWork(_db);

        _testUser = new ApplicationUser
        {
            Id = _userId,
            UserName = "authuser@test.com",
            NormalizedUserName = "AUTHUSER@TEST.COM",
            Email = "authuser@test.com",
            NormalizedEmail = "AUTHUSER@TEST.COM",
            FullName = "Auth User",
            Status = UserStatus.Active
        };
        _db.Users.Add(_testUser);
        _db.SaveChanges();

        _mockUserManager.Setup(m => m.FindByIdAsync(_userId.ToString()))
            .ReturnsAsync(_testUser);
    }

    public void Dispose() => _db.Dispose();

    private RefreshTokenCommandHandler CreateHandler()
    {
        return new RefreshTokenCommandHandler(
            _mockUserManager.Object,
            _unitOfWork,
            _mockTokenService.Object,
            _mockTokenIssuer.Object,
            _mockLoc.Object,
            _mockLogger.Object);
    }

    [Fact(DisplayName = "TC-AUTH-01: Non-existent refresh token returns failure")]
    public async Task RefreshToken_NonExistentToken_ReturnsFail()
    {
        var handler = CreateHandler();
        var command = new RefreshTokenCommand("non-existent-token", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("InvalidRefreshToken");
    }

    [Fact(DisplayName = "TC-AUTH-02: Expired refresh token returns RefreshTokenExpired")]
    public async Task RefreshToken_ExpiredToken_ReturnsFail()
    {
        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Token = "expired-token-xyz",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), // Expired
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-8),
            CreatedByIp = "127.0.0.1"
        };
        _db.RefreshTokens.Add(expiredToken);
        await _db.SaveChangesAsync();

        var handler = CreateHandler();
        var command = new RefreshTokenCommand("expired-token-xyz", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("RefreshTokenExpired");
    }

    [Fact(DisplayName = "TC-AUTH-03: Replay of revoked token detects reuse and invalidates all sibling sessions")]
    public async Task RefreshToken_ReplayRevokedToken_RevokesAllUserSessions()
    {
        // 1. A previously revoked token (e.g. stolen from browser history)
        var revokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Token = "stolen-revoked-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByIp = "127.0.0.1",
            RevokedAt = DateTimeOffset.UtcNow.AddHours(-2), // Already revoked!
            RevokedByIp = "127.0.0.1"
        };

        // 2. An active sibling session on another device
        var activeSiblingToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Token = "legitimate-active-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = "192.168.1.100"
        };

        _db.RefreshTokens.AddRange(revokedToken, activeSiblingToken);
        await _db.SaveChangesAsync();

        var handler = CreateHandler();
        var command = new RefreshTokenCommand("stolen-revoked-token", "10.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        // Assert request is rejected
        result.Success.Should().BeFalse();
        result.Message.Should().Be("RefreshTokenExpired");

        // Assert active sibling token was revoked as a security precaution
        var siblingInDb = await _db.RefreshTokens.FindAsync(activeSiblingToken.Id);
        siblingInDb!.IsRevoked.Should().BeTrue();
        siblingInDb.RevokedByIp.Should().Be("10.0.0.1");
    }

    [Fact(DisplayName = "TC-AUTH-04: Valid active refresh token rotates properly and issues new token pair")]
    public async Task RefreshToken_ValidActiveToken_RotatesTokenAndReturnsNewAuthResponse()
    {
        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Token = "valid-active-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = "127.0.0.1"
        };
        _db.RefreshTokens.Add(activeToken);
        await _db.SaveChangesAsync();

        _mockTokenService.Setup(s => s.GenerateRefreshToken())
            .Returns("brand-new-rotated-token");

        _mockTokenIssuer.Setup(i => i.IssueTokensAsync(
            _testUser, "127.0.0.1", It.IsAny<CancellationToken>(), "brand-new-rotated-token"))
            .ReturnsAsync(new AuthResponse
            {
                AccessToken = "jwt-access-token",
                RefreshToken = "brand-new-rotated-token",
                User = new UserDto
                {
                    Id = _userId,
                    Email = _testUser.Email,
                    FullName = _testUser.FullName
                }
            });

        var handler = CreateHandler();
        var command = new RefreshTokenCommand("valid-active-token", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("jwt-access-token");
        result.Data.RefreshToken.Should().Be("brand-new-rotated-token");

        // Assert the old token was revoked & marked as replaced
        var oldTokenInDb = await _db.RefreshTokens.FindAsync(activeToken.Id);
        oldTokenInDb!.IsRevoked.Should().BeTrue();
        oldTokenInDb.ReplacedByToken.Should().Be("brand-new-rotated-token");
    }

    [Fact(DisplayName = "TC-AUTH-05: Suspended user attempting token refresh is blocked")]
    public async Task RefreshToken_SuspendedUser_ReturnsAccountNotAvailable()
    {
        _testUser.Status = UserStatus.Suspended;

        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Token = "suspended-user-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = "127.0.0.1"
        };
        _db.RefreshTokens.Add(activeToken);
        await _db.SaveChangesAsync();

        var handler = CreateHandler();
        var command = new RefreshTokenCommand("suspended-user-token", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("AccountNotAvailable");
    }
}
