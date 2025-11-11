using AutoTrader.UI.Commands;
using FluentAssertions;

namespace AutoTrader.UI.Tests.Commands;

public class RelayCommandTests
{
    [Fact]
    public void Execute_WhenActionProvided_ShouldInvokeAction()
    {
        // Arrange
        var executed = false;
        var command = new RelayCommand(_ => executed = true);

        // Act
        command.Execute(null);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public void Execute_WithParameter_ShouldPassParameter()
    {
        // Arrange
        object? receivedParameter = null;
        var command = new RelayCommand(param => receivedParameter = param);
        var testParameter = new object();

        // Act
        command.Execute(testParameter);

        // Assert
        receivedParameter.Should().Be(testParameter);
    }

    [Fact]
    public void CanExecute_WhenPredicateNotProvided_ShouldReturnTrue()
    {
        // Arrange
        var command = new RelayCommand(_ => { });

        // Act
        var canExecute = command.CanExecute(null);

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void CanExecute_WhenPredicateProvided_ShouldReturnPredicateResult()
    {
        // Arrange
        var command = new RelayCommand(_ => { }, param => (int?)param > 0);

        // Act
        var canExecuteTrue = command.CanExecute(5);
        var canExecuteFalse = command.CanExecute(-1);

        // Assert
        canExecuteTrue.Should().BeTrue();
        canExecuteFalse.Should().BeFalse();
    }

    [Fact]
    public void CanExecute_WithNullParameter_ShouldHandleGracefully()
    {
        // Arrange
        var command = new RelayCommand(_ => { }, param => param != null);

        // Act
        var canExecute = command.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse();
    }
}

/// <summary>
/// RelayCommand&lt;T&gt; 제네릭 커맨드 단위 테스트
/// </summary>
public class RelayCommandGenericTests
{
    [Fact]
    public void Constructor_WithNullExecute_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new RelayCommand<string>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("execute");
    }

    [Fact]
    public void Execute_ShouldInvokeActionWithTypedParameter()
    {
        // Arrange
        string? receivedValue = null;
        var command = new RelayCommand<string>(value => receivedValue = value);
        var testValue = "test value";

        // Act
        command.Execute(testValue);

        // Assert
        receivedValue.Should().Be(testValue);
    }

    [Fact]
    public void Execute_WithNullParameter_ShouldNotThrow()
    {
        // Arrange
        string? receivedValue = "initial";
        var command = new RelayCommand<string>(value => receivedValue = value);

        // Act
        var act = () => command.Execute(null);

        // Assert
        act.Should().NotThrow();
        receivedValue.Should().BeNull();
    }

    [Fact]
    public void CanExecute_WithoutPredicate_ShouldReturnTrue()
    {
        // Arrange
        var command = new RelayCommand<string>(_ => { });

        // Act
        var canExecute = command.CanExecute("test");

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void CanExecute_WithPredicate_ShouldReturnPredicateResult()
    {
        // Arrange
        var command = new RelayCommand<int>(
            value => { },
            value => value > 0
        );

        // Act
        var result1 = command.CanExecute(5);
        var result2 = command.CanExecute(-1);
        var result3 = command.CanExecute(0);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
    }

    [Fact]
    public void Execute_WithComplexType_ShouldWork()
    {
        // Arrange
        TestViewModel? receivedObject = null;
        var command = new RelayCommand<TestViewModel>(obj => receivedObject = obj);
        var testObject = new TestViewModel { Id = 1, Name = "Test" };

        // Act
        command.Execute(testObject);

        // Assert
        receivedObject.Should().BeSameAs(testObject);
        receivedObject!.Id.Should().Be(1);
        receivedObject.Name.Should().Be("Test");
    }

    [Fact]
    public void CanExecute_WithComplexTypePredicate_ShouldWork()
    {
        // Arrange
        var command = new RelayCommand<TestViewModel>(
            obj => { },
            obj => obj != null && obj.Id > 0
        );

        var validObject = new TestViewModel { Id = 1, Name = "Valid" };
        var invalidObject = new TestViewModel { Id = 0, Name = "Invalid" };

        // Act
        var result1 = command.CanExecute(validObject);
        var result2 = command.CanExecute(invalidObject);
        var result3 = command.CanExecute(null);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
    }

    [Fact]
    public void Execute_WithObjectParameter_ShouldCastCorrectly()
    {
        // Arrange
        int? receivedValue = null;
        var command = new RelayCommand<int>(value => receivedValue = value);

        // Act
        command.Execute((object)42);

        // Assert
        receivedValue.Should().Be(42);
    }

    [Fact]
    public void MultipleExecutions_ShouldWorkCorrectly()
    {
        // Arrange
        var executionCount = 0;
        var lastValue = string.Empty;
        var command = new RelayCommand<string>(value =>
        {
            executionCount++;
            lastValue = value ?? string.Empty;
        });

        // Act
        command.Execute("test1");
        command.Execute("test2");
        command.Execute("test3");

        // Assert
        executionCount.Should().Be(3);
        lastValue.Should().Be("test3");
    }

    [Fact]
    public void CanExecute_DynamicPredicate_ShouldEvaluateEachTime()
    {
        // Arrange
        var threshold = 10;
        var command = new RelayCommand<int>(
            value => { },
            value => value > threshold
        );

        // Act & Assert
        command.CanExecute(15).Should().BeTrue();
        command.CanExecute(5).Should().BeFalse();

        threshold = 3;
        command.CanExecute(5).Should().BeTrue();
        command.CanExecute(2).Should().BeFalse();
    }

    #region Helper Classes

    private class TestViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
