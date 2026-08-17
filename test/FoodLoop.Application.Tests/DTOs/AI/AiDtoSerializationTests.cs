using System;
using System.Text.Json;
using FluentAssertions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Extensions;
using FoodLoop.Domain.Enums;
using Xunit;

namespace FoodLoop.Application.Tests.DTOs.AI;

public class AiDtoSerializationTests
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void MonitoringResponseDto_should_deserialize_correctly_from_literal_fixture()
    {
        // Arrange
        const string json = @"{ ""route"": ""PRICING"", ""risk_level"": ""HIGH"", ""reason"": ""High inventory coverage with only 18 hours remaining."", ""confidence"": 0.93 }";

        // Act
        var dto = JsonSerializer.Deserialize<MonitoringResponseDto>(json, _serializerOptions);

        // Assert
        dto.Should().NotBeNull();
        dto!.Route.Should().Be("PRICING");
        dto.RiskLevel.Should().Be("HIGH");
        dto.Reason.Should().Be("High inventory coverage with only 18 hours remaining.");
        dto.Confidence.Should().Be(0.93);
    }

    [Fact]
    public void PricingBatchResponseDto_should_deserialize_correctly_from_literal_fixture()
    {
        // Arrange
        const string json = @"{ ""store_id"": ""store-cairo-01"", ""decisions"": [ { ""product_id"": ""p-100"", ""discount_percentage"": 10.0, ""reason"": ""High inventory coverage and short remaining shelf life support a moderate markdown."", ""confidence"": 0.92, ""action_requirement"": ""APPROVAL_REQUIRED"", ""action_reason"": ""Store operates in assisted mode; explicit owner approval is required before execution."" } ] }";

        // Act
        var dto = JsonSerializer.Deserialize<PricingBatchResponseDto>(json, _serializerOptions);

        // Assert
        dto.Should().NotBeNull();
        dto!.StoreId.Should().Be("store-cairo-01");
        dto.Decisions.Should().HaveCount(1);
        
        var decision = dto.Decisions[0];
        decision.ProductId.Should().Be("p-100");
        decision.DiscountPercentage.Should().Be(10.0);
        decision.Reason.Should().Be("High inventory coverage and short remaining shelf life support a moderate markdown.");
        decision.Confidence.Should().Be(0.92);
        decision.ActionRequirement.Should().Be("APPROVAL_REQUIRED");
        decision.ActionReason.Should().Be("Store operates in assisted mode; explicit owner approval is required before execution.");
    }

    [Fact]
    public void ToApiOperatingMode_should_return_lowercase_string_for_assisted_and_autonomous()
    {
        // Act & Assert
        AiOperatingMode.Assisted.ToApiOperatingMode().Should().Be("assisted");
        AiOperatingMode.Autonomous.ToApiOperatingMode().Should().Be("autonomous");
    }

    [Fact]
    public void ToApiOperatingMode_should_throw_InvalidOperationException_for_manual_mode()
    {
        // Arrange
        var mode = AiOperatingMode.Manual;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => mode.ToApiOperatingMode());
        ex.Message.Should().Contain("Manual operating mode are forbidden");
    }

    [Fact]
    public void PricingBatchRequestDto_should_deserialize_and_serialize_correctly_against_literal_fixture()
    {
        // Arrange
        const string json = @"{
            ""store_id"": ""store-cairo-01"",
            ""store_policy"": {
                ""store_id"": ""store-cairo-01"",
                ""operating_mode"": ""assisted""
            },
            ""products"": [
                {
                    ""product_id"": ""p-100"",
                    ""product_name"": ""Organic Milk 1L"",
                    ""category"": ""Dairy"",
                    ""inventory"": {
                        ""quantity"": 10,
                        ""original_price"": 40.0,
                        ""current_price"": 40.0,
                        ""price_floor"": 28.0
                    },
                    ""demand"": {
                        ""sales_velocity"": 0.5,
                        ""historical_sales"": {
                            ""average_daily_sales"": 5.0
                        }
                    },
                    ""expiry"": {
                        ""expires_at"": ""2026-08-16T12:00:00Z"",
                        ""hours_remaining"": 18.0
                    },
                    ""risk_assessment"": {
                        ""risk_level"": ""HIGH"",
                        ""reason"": ""Short remaining shelf life."",
                        ""confidence"": 0.93
                    }
                }
            ]
        }";

        // Act - Deserialize
        var dto = JsonSerializer.Deserialize<PricingBatchRequestDto>(json, _serializerOptions);

        // Assert - Field values match exactly
        dto.Should().NotBeNull();
        dto!.StoreId.Should().Be("store-cairo-01");
        
        dto.StorePolicy.Should().NotBeNull();
        dto.StorePolicy.StoreId.Should().Be("store-cairo-01");
        dto.StorePolicy.OperatingMode.Should().Be("assisted");

        dto.Products.Should().HaveCount(1);
        var product = dto.Products[0];
        product.ProductId.Should().Be("p-100");
        product.ProductName.Should().Be("Organic Milk 1L");
        product.Category.Should().Be("Dairy");

        product.Inventory.Should().NotBeNull();
        product.Inventory.Quantity.Should().Be(10);
        product.Inventory.OriginalPrice.Should().Be(40.0m);
        product.Inventory.CurrentPrice.Should().Be(40.0m);
        product.Inventory.PriceFloor.Should().Be(28.0m);

        product.Demand.Should().NotBeNull();
        product.Demand.SalesVelocity.Should().Be(0.5);
        product.Demand.HistoricalSales.Should().NotBeNull();
        product.Demand.HistoricalSales.AverageDailySales.Should().Be(5.0);

        product.Expiry.Should().NotBeNull();
        product.Expiry.ExpiresAt.Should().Be(DateTimeOffset.Parse("2026-08-16T12:00:00Z"));
        product.Expiry.HoursRemaining.Should().Be(18.0);

        product.RiskAssessment.Should().NotBeNull();
        product.RiskAssessment.RiskLevel.Should().Be("HIGH");
        product.RiskAssessment.Reason.Should().Be("Short remaining shelf life.");
        product.RiskAssessment.Confidence.Should().Be(0.93);

        // Act - Serialize
        var roundTripJson = JsonSerializer.Serialize(dto, _serializerOptions);
        var roundTripDto = JsonSerializer.Deserialize<PricingBatchRequestDto>(roundTripJson, _serializerOptions);

        // Assert - Round-tripped matches exactly
        roundTripDto.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void MonitoringRequestDto_should_serialize_correctly_to_snake_case_contract()
    {
        // Arrange
        var request = new MonitoringRequestDto(
            Product: new MonitoringProductDto("p-10", "Milk", "Dairy"),
            Inventory: new MonitoringInventoryDto(5, 20.0m, 18.0m, 15.0m),
            Demand: new MonitoringDemandDto(1.5, new MonitoringHistoricalSalesDto(2.0)),
            Expiry: new MonitoringExpiryDto(DateTimeOffset.Parse("2026-08-16T12:00:00Z"), 6.5),
            Location: new MonitoringLocationDto(30.0, 31.2, "store-1"),
            StorePolicy: new MonitoringStorePolicyDto("store-1", "autonomous"),
            Timestamp: DateTimeOffset.Parse("2026-08-16T05:30:00Z")
        );

        // Act
        var json = JsonSerializer.Serialize(request, _serializerOptions);

        // Assert - Confirm exact snake_case JSON shape
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("product", out var productProp).Should().BeTrue();
        productProp.GetProperty("id").GetString().Should().Be("p-10");
        productProp.GetProperty("name").GetString().Should().Be("Milk");
        productProp.GetProperty("category").GetString().Should().Be("Dairy");

        root.TryGetProperty("inventory", out var invProp).Should().BeTrue();
        invProp.GetProperty("quantity").GetInt32().Should().Be(5);
        invProp.GetProperty("original_price").GetDecimal().Should().Be(20.0m);
        invProp.GetProperty("current_price").GetDecimal().Should().Be(18.0m);
        invProp.GetProperty("price_floor").GetDecimal().Should().Be(15.0m);

        root.TryGetProperty("demand", out var demandProp).Should().BeTrue();
        demandProp.GetProperty("sales_velocity").GetDouble().Should().Be(1.5);
        demandProp.GetProperty("historical_sales").GetProperty("average_daily_sales").GetDouble().Should().Be(2.0);

        root.TryGetProperty("expiry", out var expiryProp).Should().BeTrue();
        expiryProp.GetProperty("expires_at").GetString().Should().Be("2026-08-16T12:00:00+00:00");
        expiryProp.GetProperty("hours_remaining").GetDouble().Should().Be(6.5);

        root.TryGetProperty("location", out var locProp).Should().BeTrue();
        locProp.GetProperty("latitude").GetDouble().Should().Be(30.0);
        locProp.GetProperty("longitude").GetDouble().Should().Be(31.2);
        locProp.GetProperty("store_id").GetString().Should().Be("store-1");

        root.TryGetProperty("store_policy", out var spProp).Should().BeTrue();
        spProp.GetProperty("store_id").GetString().Should().Be("store-1");
        spProp.GetProperty("operating_mode").GetString().Should().Be("autonomous");

        root.TryGetProperty("timestamp", out var tsProp).Should().BeTrue();
        tsProp.GetString().Should().Be("2026-08-16T05:30:00+00:00");
    }
}
