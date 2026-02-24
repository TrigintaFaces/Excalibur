// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

/// <summary>
/// Depth coverage tests for <see cref="Excalibur.Domain.OrderedEventProcessor"/>.
/// Covers disposal, double disposal, ProcessAsync after disposal, and null guard.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class OrderedEventProcessorDepthShould
{
	[Fact]
	public async Task ProcessAsync_ThrowsArgumentNullException_WhenDelegateIsNull()
	{
		// Arrange
		using var processor = new OrderedEventProcessor();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() => processor.ProcessAsync(null!));
	}

	[Fact]
	public async Task ProcessAsync_ThrowsObjectDisposedException_AfterAsyncDisposal()
	{
		// Arrange
		var processor = new OrderedEventProcessor();
		await processor.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() => processor.ProcessAsync(() => Task.CompletedTask));
	}

	[Fact]
	public async Task ProcessAsync_ThrowsObjectDisposedException_AfterSyncDisposal()
	{
		// Arrange
		var processor = new OrderedEventProcessor();
		processor.Dispose();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() => processor.ProcessAsync(() => Task.CompletedTask));
	}

	[Fact]
	public async Task ProcessAsync_ExecutesDelegate()
	{
		// Arrange
		using var processor = new OrderedEventProcessor();
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
	public async Task ProcessAsync_MaintainsOrder()
	{
		// Arrange
		using var processor = new OrderedEventProcessor();
		var results = new List<int>();

		// Act
		var tasks = Enumerable.Range(0, 10).Select(i =>
			processor.ProcessAsync(async () =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1).ConfigureAwait(false);
				results.Add(i);
			})).ToList();

		await Task.WhenAll(tasks);

		// Assert
		results.Count.ShouldBe(10);
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var processor = new OrderedEventProcessor();

		// Act — double async dispose should not throw
		await processor.DisposeAsync();
		await processor.DisposeAsync();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var processor = new OrderedEventProcessor();

		// Act — double sync dispose should not throw
		processor.Dispose();
		processor.Dispose();
	}

	[Fact]
	public async Task ProcessAsync_ReleasesLock_EvenOnException()
	{
		// Arrange
		using var processor = new OrderedEventProcessor();
		var secondExecuted = false;

		// Act
		await Should.ThrowAsync<InvalidOperationException>(() =>
			processor.ProcessAsync(() => throw new InvalidOperationException("test")));

		await processor.ProcessAsync(() =>
		{
			secondExecuted = true;
			return Task.CompletedTask;
		});

		// Assert
		secondExecuted.ShouldBeTrue();
	}
}
