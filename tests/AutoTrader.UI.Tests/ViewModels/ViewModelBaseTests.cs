using AutoTrader.UI.ViewModels;
using FluentAssertions;

namespace AutoTrader.UI.Tests.ViewModels;

public class ViewModelBaseTests
{
    private class TestViewModel : ViewModelBase
    {
        private string _testProperty = string.Empty;

        public string TestProperty
        {
            get => _testProperty;
            set => SetProperty(ref _testProperty, value);
        }
    }

    [Fact]
    public void SetProperty_WhenValueChanges_ShouldRaisePropertyChangedEvent()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var eventRaised = false;
        string? propertyName = null;

        viewModel.PropertyChanged += (sender, e) =>
        {
            eventRaised = true;
            propertyName = e.PropertyName;
        };

        // Act
        viewModel.TestProperty = "New Value";

        // Assert
        eventRaised.Should().BeTrue();
        propertyName.Should().Be(nameof(TestViewModel.TestProperty));
        viewModel.TestProperty.Should().Be("New Value");
    }

    [Fact]
    public void SetProperty_WhenValueIsSame_ShouldNotRaisePropertyChangedEvent()
    {
        // Arrange
        var viewModel = new TestViewModel();
        viewModel.TestProperty = "Initial Value";

        var eventRaised = false;
        viewModel.PropertyChanged += (sender, e) => eventRaised = true;

        // Act
        viewModel.TestProperty = "Initial Value";

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void SetProperty_MultipleChanges_ShouldRaiseEventForEachChange()
    {
        // Arrange
        var viewModel = new TestViewModel();
        var eventCount = 0;

        viewModel.PropertyChanged += (sender, e) => eventCount++;

        // Act
        viewModel.TestProperty = "Value1";
        viewModel.TestProperty = "Value2";
        viewModel.TestProperty = "Value3";

        // Assert
        eventCount.Should().Be(3);
    }
}
