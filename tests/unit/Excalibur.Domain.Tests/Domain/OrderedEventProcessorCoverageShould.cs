namespace Excalibur.Tests.Domain;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class OrderedEventProcessorCoverageShould
{
    [Fact]
    public async Task ProcessAsync_ExecuteDelegate()
    {
        // Arrange
        await using var processor = new OrderedEventProcessor();
        var executed = false;

        // Act
        await processor.ProcessAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        // Assert
        executed.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessAsync_ThrowOnNullDelegate()
    {
        // Arrange
        await using var processor = new OrderedEventProcessor();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => processor.ProcessAsync(null!));
    }

    [Fact]
    public async Task ProcessAsync_ThrowAfterDispose()
    {
        // Arrange
        var processor = new OrderedEventProcessor();
        await processor.DisposeAsync();

        // Act & Assert
        await Should.ThrowAsync<ObjectDisposedException>(() =>
            processor.ProcessAsync(() => Task.CompletedTask));
    }

    [Fact]
    public async Task ProcessAsync_MaintainOrder()
    {
        // Arrange
        await using var processor = new OrderedEventProcessor();
        var order = new List<int>();
        var barrier = new TaskCompletionSource();

        // Act - start two overlapping tasks
        var task1 = processor.ProcessAsync(async () =>
        {
            order.Add(1);
            await barrier.Task;
            order.Add(2);
        });

        // Give task1 a chance to acquire the semaphore
        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

        var task2 = Task.Run(() => processor.ProcessAsync(() =>
        {
            order.Add(3);
            return Task.CompletedTask;
        }));

        // Complete barrier so task1 finishes
        barrier.SetResult();
        await task1;
        await task2;

        // Assert - order should be sequential
        order[0].ShouldBe(1);
        order[1].ShouldBe(2);
        order[2].ShouldBe(3);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var processor = new OrderedEventProcessor();

        // Act & Assert - should not throw
        await processor.DisposeAsync();
        await processor.DisposeAsync();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var processor = new OrderedEventProcessor();

        // Act & Assert - should not throw
        processor.Dispose();
        processor.Dispose();
    }

    [Fact]
    public void Dispose_AfterDisposeAsync_DoesNotThrow()
    {
        // Arrange
        var processor = new OrderedEventProcessor();
        processor.DisposeAsync().AsTask().GetAwaiter().GetResult();

        // Act & Assert - should not throw
        processor.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_PropagateExceptions()
    {
        // Arrange
        await using var processor = new OrderedEventProcessor();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(() => throw new InvalidOperationException("test error")));
    }

    [Fact]
    public async Task ProcessAsync_ReleasesSemaphoreAfterException()
    {
        // Arrange
        await using var processor = new OrderedEventProcessor();

        // Act - first call throws
        await Should.ThrowAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(() => throw new InvalidOperationException("fail")));

        // Second call should succeed (semaphore was released)
        var succeeded = false;
        await processor.ProcessAsync(() =>
        {
            succeeded = true;
            return Task.CompletedTask;
        });

        // Assert
        succeeded.ShouldBeTrue();
    }
}
