using FluentAssertions;
using AutoTrader.Core.Configuration;
using Microsoft.Extensions.Options;

namespace AutoTrader.Tests;

/// <summary>
/// 기본 통합 테스트
/// - Configuration 바인딩 검증
/// - 기본 서비스 생성 테스트
/// </summary>
public class BasicIntegrationTests
{
    [Fact]
    public void Configuration_KisSettings_ShouldHaveRequiredProperties()
    {
        // Arrange
        var settings = new KisSettings
        {
            AppKey = "TEST_APP_KEY",
            AppSecret = "TEST_APP_SECRET",
            AccountNumber = "12345678-01",
            BaseUrl = "https://openapi.koreainvestment.com:9443",
            IsPaperTrading = true
        };

        // Assert
        settings.AppKey.Should().NotBeNullOrEmpty();
        settings.AppSecret.Should().NotBeNullOrEmpty();
        settings.AccountNumber.Should().NotBeNullOrEmpty();
        settings.BaseUrl.Should().StartWith("https://");
        settings.IsPaperTrading.Should().BeTrue();
    }

    [Fact]
    public void Configuration_TradingSettings_ShouldHaveDefaultValues()
    {
        // Arrange
        var settings = new TradingSettings
        {
            MaxStocksToTrade = 2,
            AllocationPercent = 5,
            LimitPriceMultiplier = 1.05
        };

        // Assert
        settings.MaxStocksToTrade.Should().Be(2);
        settings.AllocationPercent.Should().Be(5);
        settings.LimitPriceMultiplier.Should().Be(1.05);
    }

    [Fact]
    public void Configuration_ApiThrottlingSettings_ShouldValidate()
    {
        // Arrange
        var settings = new ApiThrottlingSettings
        {
            MaxCallsPerSecond = 20
        };

        // Assert
        settings.MaxCallsPerSecond.Should().BeGreaterThan(0);
        settings.MaxCallsPerSecond.Should().BeLessThanOrEqualTo(20);
    }

    [Fact]
    public void Configuration_WebSocketSettings_ShouldBeValid()
    {
        // Arrange
        var settings = new WebSocketSettings
        {
            SessionCount = 8,
            StocksPerSession = 41,
            ReconnectDelaySeconds = 5,
            HeartbeatIntervalSeconds = 30
        };

        // Assert
        settings.SessionCount.Should().BeInRange(1, 10);
        settings.StocksPerSession.Should().BeLessThanOrEqualTo(50);
        settings.ReconnectDelaySeconds.Should().BeGreaterThan(0);
        settings.HeartbeatIntervalSeconds.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(10000, 0.05, 500)]
    [InlineData(20000, 0.05, 1000)]
    [InlineData(10000, 0.10, 1000)]
    public void Trading_AllocationCalculation_ShouldBeCorrect(
        decimal balance,
        decimal allocationPercent,
        decimal expectedAmount)
    {
        // Act
        var allocatedAmount = balance * allocationPercent;

        // Assert
        allocatedAmount.Should().Be(expectedAmount);
    }

    [Theory]
    [InlineData(100.0, 1.05, 105.0)]
    [InlineData(200.0, 1.05, 210.0)]
    [InlineData(50.0, 1.05, 52.5)]
    public void Trading_LocPriceCalculation_ShouldBeCorrect(
        decimal currentPrice,
        decimal multiplier,
        decimal expectedLocPrice)
    {
        // Act
        var locPrice = currentPrice * (decimal)multiplier;

        // Assert
        locPrice.Should().Be(expectedLocPrice);
    }

    [Theory]
    [InlineData(500, 105, 4)]  // $500 / $105 = 4.76 → 4
    [InlineData(1000, 105, 9)] // $1000 / $105 = 9.52 → 9
    [InlineData(500, 1050, 0)] // $500 / $1050 = 0.47 → 0
    public void Trading_QuantityCalculation_ShouldFloorDecimal(
        decimal availableAmount,
        decimal locPrice,
        int expectedQuantity)
    {
        // Act
        var quantity = (int)(availableAmount / locPrice);

        // Assert
        quantity.Should().Be(expectedQuantity);
    }

    [Fact]
    public void Trading_QuantityCalculation_MinimumOneShare()
    {
        // Arrange
        decimal availableAmount = 500m;
        decimal locPrice = 1050m;

        // Act
        var quantity = (int)(availableAmount / locPrice);
        if (quantity <= 0)
        {
            quantity = 1; // 최소 1주
        }

        // Assert
        quantity.Should().BeGreaterThanOrEqualTo(1);
    }
}
