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
		}).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ProcessAsync_ThrowsArgumentNullException_WhenDelegateIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_processor.ProcessAsync(null!)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		await _processor.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			_processor.ProcessAsync(() => Task.CompletedTask)).ConfigureAwait(false);
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
			}));
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
			_processor.ProcessAsync(() => throw expectedException)).ConfigureAwait(false);

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
			}).ConfigureAwait(false);
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
		}).ConfigureAwait(false);

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
			processor.ProcessAsync(() => Task.CompletedTask)).ConfigureAwait(false);
	}
}

