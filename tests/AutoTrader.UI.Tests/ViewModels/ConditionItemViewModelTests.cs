using AutoTrader.UI.Models;
using AutoTrader.UI.ViewModels;
using FluentAssertions;
using System.ComponentModel;

namespace AutoTrader.UI.Tests.ViewModels;

/// <summary>
/// ConditionItemViewModel 단위 테스트
/// </summary>
public class ConditionItemViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var viewModel = new ConditionItemViewModel();

        // Assert
        viewModel.Id.Should().BeEmpty();
        viewModel.IsEnabled.Should().BeTrue(); // Default is true
        viewModel.Type.Should().Be(default(ConditionType));
        viewModel.Description.Should().BeEmpty();
        viewModel.IsConditionMet.Should().BeFalse();
        viewModel.StatusMessage.Should().BeEmpty();
        viewModel.Parameters.Should().NotBeNull();
        viewModel.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Id_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ConditionItemViewModel.Id))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.Id = "A";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.Id.Should().Be("A");
    }

    [Fact]
    public void IsEnabled_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ConditionItemViewModel.IsEnabled))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IsEnabled = false; // Change from default value (true)

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Type_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel { Type = ConditionType.PriceChange };
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ConditionItemViewModel.Type))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.Type = ConditionType.MovingAverage; // Change to different value

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.Type.Should().Be(ConditionType.MovingAverage);
    }

    [Fact]
    public void Description_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ConditionItemViewModel.Description))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.Description = "Test description";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.Description.Should().Be("Test description");
    }

    [Fact]
    public void IsConditionMet_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ConditionItemViewModel.IsConditionMet))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IsConditionMet = true;

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.IsConditionMet.Should().BeTrue();
    }

    [Fact]
    public void StatusMessage_WhenSet_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ConditionItemViewModel.StatusMessage))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.StatusMessage = "✅ 충족";

        // Assert
        propertyChangedRaised.Should().BeTrue();
        viewModel.StatusMessage.Should().Be("✅ 충족");
    }

    [Fact]
    public void Parameters_CanStoreAndRetrieveValues()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel();

        // Act
        viewModel.Parameters["minPercent"] = -7.0;
        viewModel.Parameters["maxPercent"] = 0.0;

        // Assert
        viewModel.Parameters.Should().HaveCount(2);
        viewModel.Parameters["minPercent"].Should().Be(-7.0);
        viewModel.Parameters["maxPercent"].Should().Be(0.0);
    }

    [Fact]
    public void AllProperties_WhenSetToSameValue_ShouldNotRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel { Id = "A" };
        var propertyChangedCount = 0;
        viewModel.PropertyChanged += (sender, args) => propertyChangedCount++;

        // Act
        viewModel.Id = "A"; // Same value

        // Assert
        propertyChangedCount.Should().Be(0);
    }

    [Fact]
    public void CompleteConditionSetup_ShouldHaveAllRequiredProperties()
    {
        // Arrange & Act
        var viewModel = new ConditionItemViewModel
        {
            Id = "A",
            IsEnabled = true,
            Type = ConditionType.PriceChange,
            Description = "등락률: [일봉] 0봉 전 종가 대비 -7.0% ~ 0.0%",
            IsConditionMet = true,
            StatusMessage = "✅ 충족 (TSLA: -5.2%)"
        };
        viewModel.Parameters["minPercent"] = -7.0;
        viewModel.Parameters["maxPercent"] = 0.0;

        // Assert
        viewModel.Id.Should().Be("A");
        viewModel.IsEnabled.Should().BeTrue();
        viewModel.Type.Should().Be(ConditionType.PriceChange);
        viewModel.Description.Should().Contain("등락률");
        viewModel.IsConditionMet.Should().BeTrue();
        viewModel.StatusMessage.Should().Contain("충족");
        viewModel.Parameters.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(ConditionType.PriceChange)]
    [InlineData(ConditionType.MovingAverage)]
    [InlineData(ConditionType.TradeVolume)]
    [InlineData(ConditionType.PriceComparison)]
    public void Type_CanBeSetToAllConditionTypes(ConditionType conditionType)
    {
        // Arrange
        var viewModel = new ConditionItemViewModel();

        // Act
        viewModel.Type = conditionType;

        // Assert
        viewModel.Type.Should().Be(conditionType);
    }

    [Fact]
    public void MultiplePropertyChanges_ShouldRaiseMultipleEvents()
    {
        // Arrange
        var viewModel = new ConditionItemViewModel();
        var propertyNames = new List<string>();
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
                propertyNames.Add(args.PropertyName);
        };

        // Act
        viewModel.Id = "A";
        viewModel.IsEnabled = false; // Change from default (true)
        viewModel.Description = "Test";

        // Assert
        propertyNames.Should().Contain(nameof(ConditionItemViewModel.Id));
        propertyNames.Should().Contain(nameof(ConditionItemViewModel.IsEnabled));
        propertyNames.Should().Contain(nameof(ConditionItemViewModel.Description));
        propertyNames.Should().HaveCount(3);
    }
}
