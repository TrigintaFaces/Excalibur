// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

[Trait("Category", "Unit")]
public class OrderedEventProcessorFunctionalShould
{
    [Fact]
    public async Task ProcessAsync_ShouldEnforceSequentialOrdering()
    {
        // Arrange
        var processor = new OrderedEventProcessor();
        var executionOrder = new List<int>();
        var events = Enumerable.Range(1, 10).ToList();

        // Act - launch multiple concurrent processing tasks
        var tasks = events.Select(i => processor.ProcessAsync(async () =>
        {
            await Task.Delay(1).ConfigureAwait(false);
            lock (executionOrder)
            {
                executionOrder.Add(i);
            }
        }));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - all events should have been processed
        executionOrder.Count.ShouldBe(10);
        // Events must be sequential (no interleaving) - each one finishes before next starts
        executionOrder.ShouldBe(executionOrder.OrderBy(x => x).ToList());
    }

    [Fact]
    public async Task ProcessAsync_WithNull_ShouldThrow()
    {
        var processor = new OrderedEventProcessor();
        await Should.ThrowAsync<ArgumentNullException>(
            () => processor.ProcessAsync(null!)).ConfigureAwait(false);
    }

    [Fact]
    public async Task ProcessAsync_AfterDispose_ShouldThrow()
    {
        var processor = new OrderedEventProcessor();
        await processor.DisposeAsync().ConfigureAwait(false);

        await Should.ThrowAsync<ObjectDisposedException>(
            () => processor.ProcessAsync(() => Task.CompletedTask)).ConfigureAwait(false);
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotent()
    {
        var processor = new OrderedEventProcessor();

        await processor.DisposeAsync().ConfigureAwait(false);
        // Second dispose should not throw
        await processor.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var processor = new OrderedEventProcessor();

        processor.Dispose();
        // Second dispose should not throw
        processor.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_ShouldReleaseLockOnException()
    {
        // Arrange
        var processor = new OrderedEventProcessor();
        var secondTaskExecuted = false;

        // Act - first task throws
        await Should.ThrowAsync<InvalidOperationException>(
            () => processor.ProcessAsync(() => throw new InvalidOperationException("Test error"))).ConfigureAwait(false);

        // Second task should still be able to execute (lock released)
        await processor.ProcessAsync(() =>
        {
            secondTaskExecuted = true;
            return Task.CompletedTask;
        }).ConfigureAwait(false);

        // Assert
        secondTaskExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessAsync_MultipleConcurrentCalls_ShouldNotDeadlock()
    {
        // Arrange
        var processor = new OrderedEventProcessor();
        var counter = 0;

        // Act - fire many concurrent tasks
        var tasks = Enumerable.Range(0, 50).Select(_ => processor.ProcessAsync(() =>
        {
            Interlocked.Increment(ref counter);
            return Task.CompletedTask;
        }));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert
        counter.ShouldBe(50);
    }
}
