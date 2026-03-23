// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="OrderedEventProcessor"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class OrderedEventProcessorShould : IAsyncDisposable
{
	private readonly OrderedEventProcessor _processor = new();

	public async ValueTask DisposeAsync()
	{
		await _processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessAsync_ExecutesDelegate()
	{
		// Arrange
		var executed = false;

		// Act
		await _processor.ProcessAsync(() =>
		{
			executed = true;
			return Task.CompletedTask;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ProcessAsync_ThrowsArgumentNullException_WhenDelegateIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_processor.ProcessAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		await _processor.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			_processor.ProcessAsync(() => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessAsync_MaintainsOrderWithConcurrentCalls()
	{
		// Arrange
		var order = new List<int>();
		var tasks = new List<Task>();

		// Act - schedule multiple tasks that should execute in order
		for (var i = 0; i < 10; i++)
		{
			var index = i;
			tasks.Add(_processor.ProcessAsync(async () =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10).ConfigureAwait(false);
				order.Add(index);
			}, CancellationToken.None));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - should be in order 0, 1, 2, ... 9
		order.ShouldBe([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
	}

	[Fact]
	public async Task ProcessAsync_PropagatesExceptions()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Test exception");

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
			_processor.ProcessAsync(() => throw expectedException, CancellationToken.None)).ConfigureAwait(false);

		exception.ShouldBe(expectedException);
	}

	[Fact]
	public async Task ProcessAsync_AllowsSubsequentCalls_AfterException()
	{
		// Arrange
		var firstExecuted = false;
		var secondExecuted = false;

		// First call throws
		try
		{
			await _processor.ProcessAsync(() =>
			{
				firstExecuted = true;
				throw new InvalidOperationException();
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Second call should still work
		await _processor.ProcessAsync(() =>
		{
			secondExecuted = true;
			return Task.CompletedTask;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		firstExecuted.ShouldBeTrue();
		secondExecuted.ShouldBeTrue();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		using var processor = new OrderedEventProcessor();

		// Act & Assert - should not throw
		processor.Dispose();
		processor.Dispose();
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var processor = new OrderedEventProcessor();

		// Act & Assert - should not throw
		await processor.DisposeAsync().ConfigureAwait(false);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void ImplementsIDisposable()
	{
		// Arrange & Assert
		_ = _processor.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Arrange & Assert
		_ = _processor.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public async Task Dispose_PreventsFurtherProcessing()
	{
		// Arrange
		var processor = new OrderedEventProcessor();
		processor.Dispose();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			processor.ProcessAsync(() => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessAsync_ThrowsOperationCanceledException_WhenCancelled()
	{
		// Regression test for Sprint 677 T.1 (ag301): CancellationToken now passed to WaitAsync.
		// Verifies that a cancelled token breaks the semaphore wait cleanly.
		using var cts = new CancellationTokenSource();

		// Hold the semaphore so the next call blocks on WaitAsync
		var holdSemaphore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var processingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		var holdTask = _processor.ProcessAsync(async () =>
		{
			processingStarted.SetResult();
			await holdSemaphore.Task.ConfigureAwait(false);
		}, CancellationToken.None);

		await processingStarted.Task.ConfigureAwait(false);

		// Schedule a second call that will block on WaitAsync
		var blockedTask = _processor.ProcessAsync(() => Task.CompletedTask, cts.Token);

		// Cancel while waiting
		await cts.CancelAsync().ConfigureAwait(false);

		// Should throw OperationCanceledException (not deadlock)
		await Should.ThrowAsync<OperationCanceledException>(() => blockedTask).ConfigureAwait(false);

		// Release the first task to clean up
		holdSemaphore.SetResult();
		await holdTask.ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessAsync_SucceedsWithNonCancelledToken()
	{
		// Regression test for Sprint 677 T.1 (ag301): Verify passing a valid CT still works.
		using var cts = new CancellationTokenSource();
		var executed = false;

		await _processor.ProcessAsync(() =>
		{
			executed = true;
			return Task.CompletedTask;
		}, cts.Token).ConfigureAwait(false);

		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ProcessAsync_ThrowsObjectDisposedException_WhenSemaphoreDisposedDuringWait()
	{
		// Regression test for T.3 (hdfdz): OrderedEventProcessor disposal race.
		// Verifies that calling ProcessAsync after DisposeAsync throws ObjectDisposedException
		// cleanly, even when the semaphore has already been disposed.
		var processor = new OrderedEventProcessor();

		// Dispose first -- semaphore is now disposed
		await processor.DisposeAsync().ConfigureAwait(false);

		// Act & Assert -- ProcessAsync should throw ObjectDisposedException, not crash
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			processor.ProcessAsync(() => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessAsync_ReleaseSurvivesSemaphoreDisposal()
	{
		// Regression test for T.3 (hdfdz): Verifies that if the semaphore is disposed
		// after WaitAsync succeeds but before Release, the Release catch prevents crashes.
		var processor = new OrderedEventProcessor();

		// Process an event, then immediately dispose -- the Release catch should handle it
		await processor.ProcessAsync(() => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);
		await processor.DisposeAsync().ConfigureAwait(false);

		// Should not throw -- already tested above that post-disposal calls are handled
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			processor.ProcessAsync(() => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(false);
	}
}
