using AutoTrader.UI.Models;
using AutoTrader.UI.Services;
using AutoTrader.UI.ViewModels;
using FluentAssertions;
using Moq;

namespace AutoTrader.UI.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly Mock<ITradingService> _mockTradingService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _mockTradingService = new Mock<ITradingService>();
        _viewModel = new MainViewModel(_mockTradingService.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Assert
        _viewModel.ApiConnectionStatus.Should().NotBeNull();
        _viewModel.AccountBalance.Should().NotBeNull();
        _viewModel.TotalAssets.Should().NotBeNull();
        _viewModel.Top300Stocks.Should().NotBeNull();
        _viewModel.CandidateStocks.Should().NotBeNull();
        _viewModel.OrderPendingStocks.Should().NotBeNull();
    }

    [Fact]
    public void AllocationPercent_ShouldRaisePropertyChanged()
    {
        // Arrange
        var eventRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.AllocationPercent))
                eventRaised = true;
        };

        // Act
        _viewModel.AllocationPercent = 10;

        // Assert
        eventRaised.Should().BeTrue();
        _viewModel.AllocationPercent.Should().Be(10);
    }

    [Fact]
    public void PreventReEntry_ShouldRaisePropertyChanged()
    {
        // Arrange
        var eventRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.PreventReEntry))
                eventRaised = true;
        };

        // Act
        _viewModel.PreventReEntry = true;

        // Assert
        eventRaised.Should().BeTrue();
        _viewModel.PreventReEntry.Should().BeTrue();
    }

    [Fact]
    public void ApiConnectionStatus_ShouldUpdateCorrectly()
    {
        // Act
        _viewModel.ApiConnectionStatus = "✅ 연결됨";

        // Assert
        _viewModel.ApiConnectionStatus.Should().Be("✅ 연결됨");
    }

    [Fact]
    public void LogText_ShouldAccumulateMessages()
    {
        // Act
        _viewModel.LogText = "[INFO] Test Message 1\n";
        _viewModel.LogText += "[INFO] Test Message 2\n";

        // Assert
        _viewModel.LogText.Should().Contain("Test Message 1");
        _viewModel.LogText.Should().Contain("Test Message 2");
    }

    [Fact]
    public void Commands_ShouldBeInitialized()
    {
        // Assert
        _viewModel.StartSystemCommand.Should().NotBeNull();
        _viewModel.StopSystemCommand.Should().NotBeNull();
        _viewModel.RefreshTop300Command.Should().NotBeNull();
        _viewModel.ClearLogCommand.Should().NotBeNull();
    }

    [Fact]
    public void TradingService_LogReceived_ShouldUpdateLogText()
    {
        // Arrange
        var initialLogLength = _viewModel.LogText.Length;

        // Act
        _mockTradingService.Raise(m => m.LogReceived += null, _mockTradingService.Object, "[TEST] Test log message");

        // Assert
        _viewModel.LogText.Should().Contain("[TEST] Test log message");
        _viewModel.LogText.Length.Should().BeGreaterThan(initialLogLength);
    }

    [Fact]
    public void MarketStatus_ShouldUpdateCorrectly()
    {
        // Act
        _viewModel.MarketStatus = "개장";
        _viewModel.MarketCloseTime = "16:00 EST";

        // Assert
        _viewModel.MarketStatus.Should().Be("개장");
        _viewModel.MarketCloseTime.Should().Be("16:00 EST");
    }

    [Fact]
    public void IsDstActive_ShouldUpdateCorrectly()
    {
        // Act
        _viewModel.IsDstActive = true;

        // Assert
        _viewModel.IsDstActive.Should().BeTrue();
    }

    [Fact]
    public void CountdownText_ShouldUpdateCorrectly()
    {
        // Act
        _viewModel.CountdownText = "05:30";

        // Assert
        _viewModel.CountdownText.Should().Be("05:30");
    }

    [Fact]
    public void AccountBalance_ShouldFormatCorrectly()
    {
        // Act
        _viewModel.AccountBalance = "$50,000.00";
        _viewModel.TotalAssets = "$100,000.00";

        // Assert
        _viewModel.AccountBalance.Should().Be("$50,000.00");
        _viewModel.TotalAssets.Should().Be("$100,000.00");
    }
}
