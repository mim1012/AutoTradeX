using AutoTrader.UI.Models;
using AutoTrader.UI.ViewModels;
using FluentAssertions;

namespace AutoTrader.UI.Tests.ViewModels;

/// <summary>
/// ConditionBuilderViewModel 단위 테스트
/// </summary>
public class ConditionBuilderViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeEmptyConditionsList()
    {
        // Act
        var viewModel = new ConditionBuilderViewModel();

        // Assert
        viewModel.Conditions.Should().BeEmpty();
        viewModel.LogicExpression.Should().BeEmpty();
        viewModel.LogicExplanation.Should().Be("조건을 추가해주세요.");
    }

    [Fact]
    public void AddConditionCommand_WhenCountIsLessThan3_ShouldReturnTrue()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Act
        var canExecute = viewModel.AddConditionCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void AddConditionCommand_WhenCountIs3_ShouldReturnFalse()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Add 3 conditions
        for (int i = 0; i < 3; i++)
        {
            viewModel.AddConditionCommand.Execute(null);
        }

        // Act
        var canExecute = viewModel.AddConditionCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse();
        viewModel.Conditions.Should().HaveCount(3);
    }

    [Fact]
    public void AddConditionCommand_ShouldAddConditionWithCorrectId()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Act
        viewModel.AddConditionCommand.Execute(null);

        // Assert
        viewModel.Conditions.Should().HaveCount(1);
        viewModel.Conditions[0].Id.Should().Be("A");
        viewModel.Conditions[0].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AddConditionCommand_ShouldAssignSequentialIds()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Act
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);

        // Assert
        viewModel.Conditions.Should().HaveCount(3);
        viewModel.Conditions[0].Id.Should().Be("A");
        viewModel.Conditions[1].Id.Should().Be("B");
        viewModel.Conditions[2].Id.Should().Be("C");
    }

    [Fact]
    public void RemoveConditionCommand_ShouldRemoveCondition()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);
        var conditionToRemove = viewModel.Conditions[0];

        // Act
        viewModel.RemoveConditionCommand.Execute(conditionToRemove);

        // Assert
        viewModel.Conditions.Should().HaveCount(1);
        viewModel.Conditions.Should().NotContain(conditionToRemove);
    }

    [Fact]
    public void RemoveConditionCommand_ShouldReassignIds()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);
        var conditionB = viewModel.Conditions[1];

        // Act
        viewModel.RemoveConditionCommand.Execute(conditionB);

        // Assert
        viewModel.Conditions.Should().HaveCount(2);
        viewModel.Conditions[0].Id.Should().Be("A");
        viewModel.Conditions[1].Id.Should().Be("B"); // C becomes B
    }

    [Fact]
    public void RemoveConditionCommand_WithNullParameter_ShouldNotThrow()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Act
        var act = () => viewModel.RemoveConditionCommand.Execute(null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateLogicExpression_WithNoConditions_ShouldShowEmptyMessage()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Assert
        viewModel.LogicExpression.Should().BeEmpty();
        viewModel.LogicExplanation.Should().Be("조건을 추가해주세요.");
    }

    [Fact]
    public void UpdateLogicExpression_WithOneEnabledCondition_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Act
        viewModel.AddConditionCommand.Execute(null);

        // Assert
        viewModel.LogicExpression.Should().Be("A");
        viewModel.LogicExplanation.Should().Be("조건 A가 충족되어야 합니다.");
    }

    [Fact]
    public void UpdateLogicExpression_WithTwoEnabledConditions_ShouldGenerateAndExpression()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Act
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);

        // Assert
        viewModel.LogicExpression.Should().Be("A and B");
        viewModel.LogicExplanation.Should().Be("조건 A, B가 모두 충족되어야 합니다.");
    }

    [Fact]
    public void UpdateLogicExpression_WithThreeEnabledConditions_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Act
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);

        // Assert
        viewModel.LogicExpression.Should().Be("A and B and C");
        viewModel.LogicExplanation.Should().Be("조건 A, B, C가 모두 충족되어야 합니다.");
    }

    [Fact]
    public void UpdateLogicExpression_WhenConditionDisabled_ShouldExcludeFromExpression()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);

        // Act
        viewModel.Conditions[1].IsEnabled = false; // Disable B

        // Assert
        viewModel.LogicExpression.Should().Be("A and C");
        viewModel.LogicExplanation.Should().Be("조건 A, C가 모두 충족되어야 합니다.");
    }

    [Fact]
    public void UpdateLogicExpression_WhenAllConditionsDisabled_ShouldShowNoActiveConditionsMessage()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);

        // Act
        viewModel.Conditions[0].IsEnabled = false;
        viewModel.Conditions[1].IsEnabled = false;

        // Assert
        viewModel.LogicExpression.Should().BeEmpty();
        viewModel.LogicExplanation.Should().Be("활성화된 조건이 없습니다.");
    }

    [Fact]
    public void PropertyChanged_WhenConditionIsEnabledChanges_ShouldUpdateLogicExpression()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);

        var initialExpression = viewModel.LogicExpression;

        // Act
        viewModel.Conditions[0].IsEnabled = false;

        // Assert
        initialExpression.Should().Be("A and B");
        viewModel.LogicExpression.Should().Be("B");
    }

    [Fact]
    public void CollectionChanged_WhenConditionAdded_ShouldSubscribeToPropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Act
        viewModel.AddConditionCommand.Execute(null);
        var condition = viewModel.Conditions[0];
        condition.IsEnabled = false;

        // Assert
        viewModel.LogicExpression.Should().BeEmpty();
        viewModel.LogicExplanation.Should().Be("활성화된 조건이 없습니다.");
    }

    [Fact]
    public void CollectionChanged_WhenConditionRemoved_ShouldUnsubscribeFromPropertyChanged()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);
        var conditionToRemove = viewModel.Conditions[0];

        // Act
        viewModel.RemoveConditionCommand.Execute(conditionToRemove);
        conditionToRemove.IsEnabled = false; // Should not affect logic expression

        // Assert
        viewModel.LogicExpression.Should().Be("A"); // Only B remains (now renamed to A)
    }

    [Fact]
    public void Commands_ShouldBeInitialized()
    {
        // Arrange & Act
        var viewModel = new ConditionBuilderViewModel();

        // Assert
        viewModel.AddConditionCommand.Should().NotBeNull();
        viewModel.EditConditionCommand.Should().NotBeNull();
        viewModel.RemoveConditionCommand.Should().NotBeNull();
        viewModel.SaveConditionsCommand.Should().NotBeNull();
        viewModel.TestConditionsCommand.Should().NotBeNull();
    }

    [Fact]
    public void AddConditionCommand_AfterRemovingConditions_ShouldBeExecutableAgain()
    {
        // Arrange
        var viewModel = new ConditionBuilderViewModel();

        // Add 3 conditions (max)
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);
        viewModel.AddConditionCommand.Execute(null);

        // Act
        viewModel.RemoveConditionCommand.Execute(viewModel.Conditions[0]);
        var canExecute = viewModel.AddConditionCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeTrue();
        viewModel.Conditions.Should().HaveCount(2);
    }
}
