# Unit Test Patterns

This directory documents test patterns and conventions for Excalibur.Dispatch.

## Naming Conventions

### File Naming
- Test file: `{ClassName}Should.cs`
- Location: `tests/unit/{Project}.Tests/{Folder}/{ClassName}Should.cs`

### Test Method Naming
Pattern: `Method_Scenario_ExpectedResult`

Examples:
- `Handle_ValidCommand_ReturnsSuccess`
- `Process_NullInput_ThrowsArgumentNullException`
- `Calculate_WithNegativeValue_ReturnsZero`
- `Save_DuplicateKey_ThrowsConflictException`

### Test Categories (Regions)
Organize tests using regions:
1. **Happy Path Tests** - Normal successful operations
2. **Edge Case Tests** - Boundary conditions and special inputs
3. **Error Handling Tests** - Exception scenarios
4. **Validation Tests** - Input validation
5. **Interaction Tests** - Verify calls to dependencies
6. **Cancellation Tests** - CancellationToken behavior

## Test Frameworks

- **xUnit** - Test runner
- **Shouldly** - Assertions
- **FakeItEasy** - Mocking

## Base Classes

All tests should inherit from:
- `UnitTestBase` - For unit tests (provides `WaitUntilAsync` and other utilities)

## General Unit Test Pattern

```csharp
public class OrderProcessorShould : UnitTestBase
{
    private readonly OrderProcessor _sut;
    private readonly IOrderRepository _repository;

    public OrderProcessorShould()
    {
        _repository = A.Fake<IOrderRepository>();
        _sut = new OrderProcessor(_repository);
    }

    [Fact]
    public async Task Process_ValidOrder_SavesAndReturnsSuccess()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Value = "test" };

        // Act
        var result = await _sut.ProcessAsync(order);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() => _repository.SaveAsync(order))
            .MustHaveHappenedOnceExactly();
    }
}
```

## Handler Test Pattern

Handler tests verify command/event/query handlers with their dependencies:

```csharp
public class CreateOrderHandlerShould : UnitTestBase
{
    private readonly CreateOrderHandler _sut;
    private readonly IOrderRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public CreateOrderHandlerShould()
    {
        _repository = A.Fake<IOrderRepository>();
        _eventPublisher = A.Fake<IEventPublisher>();
        _sut = new CreateOrderHandler(_repository, _eventPublisher);
    }

    #region Handle Tests

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        // Arrange
        var command = new CreateOrderCommand { Id = Guid.NewGuid(), Value = "test" };
        A.CallTo(() => _repository.SaveAsync(A<Order>._))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesEntity()
    {
        // Arrange
        var command = new CreateOrderCommand { Id = Guid.NewGuid(), Value = "test" };

        // Act
        _ = await _sut.HandleAsync(command, CancellationToken.None);

        // Assert
        A.CallTo(() => _repository.SaveAsync(
            A<Order>.That.Matches(e => e.Id == command.Id)))
            .MustHaveHappenedOnceExactly();
    }

    #endregion Handle Tests

    #region Validation Tests

    [Fact]
    public async Task Handle_EmptyId_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand { Id = Guid.Empty, Value = "test" };

        // Act
        var result = await _sut.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_InvalidValue_ReturnsValidationError(string? value)
    {
        // Arrange
        var command = new CreateOrderCommand { Id = Guid.NewGuid(), Value = value! };

        // Act
        var result = await _sut.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    #endregion Validation Tests

    #region Cancellation Tests

    [Fact]
    public async Task Handle_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var command = new CreateOrderCommand { Id = Guid.NewGuid(), Value = "test" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.HandleAsync(command, cts.Token));
    }

    #endregion Cancellation Tests

    #region Error Handling Tests

    [Fact]
    public async Task Handle_RepositoryFails_ReturnsError()
    {
        // Arrange
        var command = new CreateOrderCommand { Id = Guid.NewGuid(), Value = "test" };
        A.CallTo(() => _repository.SaveAsync(A<Order>._))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _sut.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    #endregion Error Handling Tests
}
```
