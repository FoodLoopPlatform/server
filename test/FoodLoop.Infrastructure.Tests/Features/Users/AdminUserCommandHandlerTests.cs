using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Users;
using FoodLoop.Infrastructure.Features.Users.Commands;
using FoodLoop.Infrastructure.Features.Users.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Users;

public class AdminUserCommandHandlerTests : IDisposable
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager = MockUserManagerFactory.Create();
    private readonly ApplicationDbContext _dbContext = ApplicationDbContextFactory.Create();
    private readonly Mock<ILocalizationService> _loc = MockLocalizationServiceFactory.Create();

    public void Dispose() => _dbContext.Dispose();

    // ---------- CreateUserCommandHandler ----------

    [Fact]
    public async Task CreateUser_should_succeed_for_valid_request()
    {
        // Arrange
        _userManager.Setup(m => m.FindByEmailAsync("new@example.com")).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRole.Customer))
            .ReturnsAsync(IdentityResult.Success);

        var handler = new CreateUserCommandHandler(_userManager.Object, _loc.Object);
        var request = new CreateUserRequest
        {
            Email = "new@example.com",
            FullName = "New User",
            Password = "Password123!",
            Role = AppRole.Customer,
            Status = "Active"
        };
        var command = new CreateUserCommand(request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Email.Should().Be("new@example.com");
        result.Data.Roles.Should().Contain(AppRole.Customer);
    }

    [Fact]
    public async Task CreateUser_should_fail_for_invalid_role()
    {
        // Arrange
        var handler = new CreateUserCommandHandler(_userManager.Object, _loc.Object);
        var request = new CreateUserRequest
        {
            Email = "new@example.com",
            FullName = "New User",
            Password = "Password123!",
            Role = "InvalidRole",
            Status = "Active"
        };
        var command = new CreateUserCommand(request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid role");
    }

    // ---------- UpdateUserCommandHandler ----------

    [Fact]
    public async Task UpdateUser_should_succeed_for_valid_profile_updates()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "old@example.com", FullName = "Old Name" };
        _userManager.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { AppRole.Customer });

        var handler = new UpdateUserCommandHandler(_userManager.Object, _loc.Object);
        var request = new UpdateUserRequest
        {
            FullName = "New Name",
            Email = "updated@example.com",
            PhoneNumber = "12345",
            Status = "Suspended"
        };
        var command = new UpdateUserCommand(user.Id, request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.FullName.Should().Be("New Name");
        result.Data.Email.Should().Be("updated@example.com");
        result.Data.PhoneNumber.Should().Be("12345");
        result.Data.Status.Should().Be(UserStatus.Suspended.ToString());
    }

    // ---------- DeleteUserCommandHandler ----------

    [Fact]
    public async Task DeleteUser_should_delete_user_successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "delete@example.com" };
        _userManager.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManager.Setup(m => m.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = new DeleteUserCommandHandler(_userManager.Object, _loc.Object);
        var command = new DeleteUserCommand(userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
    }

    // ---------- GetUserByIdQueryHandler ----------

    [Fact]
    public async Task GetUserById_should_retrieve_user_dto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "get@example.com", FullName = "Get Me" };
        _userManager.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { AppRole.Charity });

        var handler = new GetUserByIdQueryHandler(_userManager.Object);
        var query = new GetUserByIdQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FullName.Should().Be("Get Me");
        result.Roles.Should().Contain(AppRole.Charity);
    }

    [Fact]
    public async Task GetUserById_should_throw_NotFoundException_when_missing()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userManager.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync((ApplicationUser?)null);

        var handler = new GetUserByIdQueryHandler(_userManager.Object);
        var query = new GetUserByIdQuery(userId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(query, CancellationToken.None));
    }

    // ---------- ListUsersQueryHandler ----------

    [Fact]
    public async Task ListUsers_should_filter_and_paginate_results_efficiently()
    {
        // Arrange
        var adminRole = new ApplicationRole(AppRole.Admin);
        var consumerRole = new ApplicationRole(AppRole.Customer);
        _dbContext.Roles.AddRange(adminRole, consumerRole);

        var user1 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user1@example.com", Email = "user1@example.com", FullName = "Amina Ahmed", Status = UserStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user2@example.com", Email = "user2@example.com", FullName = "John Doe", Status = UserStatus.Suspended, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1) };
        _dbContext.Users.AddRange(user1, user2);

        // Map roles
        _dbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = user1.Id, RoleId = consumerRole.Id },
            new IdentityUserRole<Guid> { UserId = user2.Id, RoleId = adminRole.Id }
        );

        await _dbContext.SaveChangesAsync();

        var handler = new ListUsersQueryHandler(_dbContext);

        // Scenario 1: Filter by Role = Customer
        var queryRole = new ListUsersQuery(Role: AppRole.Customer);
        var resultRole = await handler.Handle(queryRole, CancellationToken.None);
        resultRole.Items.Should().HaveCount(1);
        resultRole.Items.First().FullName.Should().Be("Amina Ahmed");

        // Scenario 2: Search term = "Doe"
        var querySearch = new ListUsersQuery(SearchTerm: "Doe");
        var resultSearch = await handler.Handle(querySearch, CancellationToken.None);
        resultSearch.Items.Should().HaveCount(1);
        resultSearch.Items.First().FullName.Should().Be("John Doe");

        // Scenario 3: Filter by Status = Suspended
        var queryStatus = new ListUsersQuery(Status: "Suspended");
        var resultStatus = await handler.Handle(queryStatus, CancellationToken.None);
        resultStatus.Items.Should().HaveCount(1);
        resultStatus.Items.First().FullName.Should().Be("John Doe");
    }
}

