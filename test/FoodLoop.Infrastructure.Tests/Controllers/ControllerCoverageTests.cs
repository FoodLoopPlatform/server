using FluentAssertions;
using FoodLoop.API.Common;
using FoodLoop.API.Controllers;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.DTOs.Products;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Application.Features.Products.Queries;
using FoodLoop.Application.Features.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Controllers;

public class ControllerCoverageTests
{
    private readonly Mock<ISender> _mockMediator = new();
    private readonly Mock<ICurrentUserService> _mockCurrentUser = new();
    private readonly Mock<ILocalizationService> _mockLoc = new();

    public ControllerCoverageTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()]).Returns<string>(s => s);
    }

    [Fact]
    public async Task AdminController_GetPendingStores_ShouldReturnOk()
    {
        // Arrange
        var stores = new List<AdminOrganizationDto>
        {
            new AdminOrganizationDto { Id = Guid.NewGuid(), Name = "Pending Mart" }
        };
        _mockMediator.Setup(m => m.Send(It.IsAny<GetPendingStoresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stores);

        var controller = new AdminController(_mockMediator.Object, _mockCurrentUser.Object);

        // Act
        var result = await controller.GetPendingStores(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<IReadOnlyList<AdminOrganizationDto>>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Should().HaveCount(1);
    }

    [Fact]
    public async Task AdminController_GetAnalyticsSummary_ShouldReturnOk()
    {
        // Arrange
        var summary = new AnalyticsSummaryDto
        {
            Users = new UserMetricsDto { Total = 100 },
            Organizations = new StoreMetricsDto { Total = 20 }
        };
        _mockMediator.Setup(m => m.Send(It.IsAny<GetAnalyticsSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var controller = new AdminController(_mockMediator.Object, _mockCurrentUser.Object);

        // Act
        var result = await controller.GetAnalyticsSummary(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<AnalyticsSummaryDto>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Users.Total.Should().Be(100);
    }

    [Fact]
    public async Task StoresController_GetMyStore_ShouldReturnOk()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        _mockCurrentUser.Setup(u => u.UserId).Returns(ownerId);

        var orgDto = new OrganizationDto
        {
            Id = Guid.NewGuid(),
            Name = "My Organic Shop"
        };
        _mockMediator.Setup(m => m.Send(It.Is<GetMyOrganizationQuery>(q => q.OwnerId == ownerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgDto);

        var controller = new StoresController(_mockMediator.Object, _mockCurrentUser.Object, _mockLoc.Object);

        // Act
        var result = await controller.GetMyStore(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<OrganizationDto>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Name.Should().Be("My Organic Shop");
    }

    [Fact]
    public async Task StoresController_GetMyStoreAnalytics_ShouldReturnOk()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        _mockCurrentUser.Setup(u => u.UserId).Returns(ownerId);

        var analyticsDto = new StoreAnalyticsDto
        {
            Revenue = 1500m,
            CompletedOrdersCount = 25
        };
        _mockMediator.Setup(m => m.Send(It.IsAny<GetStoreAnalyticsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analyticsDto);

        var controller = new StoresController(_mockMediator.Object, _mockCurrentUser.Object, _mockLoc.Object);

        // Act
        var result = await controller.GetMyStoreAnalytics("month", CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<StoreAnalyticsDto>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.Revenue.Should().Be(1500m);
    }

    [Fact]
    public async Task UsersController_GetMe_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUser.Setup(u => u.UserId).Returns(userId);

        var userDto = new UserDto
        {
            Id = userId,
            FullName = "Tarek Mostafa",
            Email = "tarek@test.com"
        };
        _mockMediator.Setup(m => m.Send(It.Is<GetCurrentUserQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userDto);

        var controller = new UsersController(_mockMediator.Object, _mockCurrentUser.Object, _mockLoc.Object);

        // Act
        var result = await controller.GetMe(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<UserDto>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.FullName.Should().Be("Tarek Mostafa");
    }

    [Fact]
    public async Task UsersController_GetMyWallet_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUser.Setup(u => u.UserId).Returns(userId);

        var walletDto = new UserWalletDto
        {
            WalletBalance = 250m,
            Transactions = new List<WalletTransactionDto>()
        };
        _mockMediator.Setup(m => m.Send(It.Is<GetUserWalletQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(walletDto);

        var controller = new UsersController(_mockMediator.Object, _mockCurrentUser.Object, _mockLoc.Object);

        // Act
        var result = await controller.GetMyWallet(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var apiResponse = okResult!.Value as ApiResponse<UserWalletDto>;
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data!.WalletBalance.Should().Be(250m);
    }
}
