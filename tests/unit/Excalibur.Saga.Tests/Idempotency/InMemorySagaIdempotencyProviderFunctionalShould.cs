// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Idempotency;

namespace Excalibur.Saga.Tests.Idempotency;

/// <summary>
/// Functional tests for <see cref="InMemorySagaIdempotencyProvider"/> covering
/// concurrent access, isolation between sagas, and lifecycle management.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemorySagaIdempotencyProviderFunctionalShould
{
	[Fact]
	public async Task HandleConcurrentMarkProcessed_ThreadSafely()
	{
		// Arrange
		var provider = new InMemorySagaIdempotencyProvider();
		var tasks = new List<Task>();

		for (var i = 0; i < 100; i++)
		{
			var sagaId = $"saga-{i % 10}";
			var key = $"key-{i}";
			tasks.Add(provider.MarkProcessedAsync(sagaId, key, CancellationToken.None));
		}

		// Act
		await Task.WhenAll(tasks);

		// Assert
		provider.Count.ShouldBe(100);
	}

	[Fact]
	public async Task HandleConcurrentIsProcessed_ThreadSafely()
	{
		// Arrange
		var provider = new InMemorySagaIdempotencyProvider();

		// Pre-populate
		for (var i = 0; i < 50; i++)
		{
			await provider.MarkProcessedAsync("saga-1", $"key-{i}", CancellationToken.None);
		}

		// Act - concurrent reads
		var tasks = new List<Task<bool>>();
		for (var i = 0; i < 50; i++)
		{
			tasks.Add(provider.IsProcessedAsync("saga-1", $"key-{i}", CancellationToken.None));
		}

		var results = await Task.WhenAll(tasks);

		// Assert - all should be true
		results.ShouldAllBe(r => r);
	}

	[Fact]
	public async Task MaintainIsolation_BetweenDifferentSagas()
	{
		// Arrange
		var provider = new InMemorySagaIdempotencyProvider();

		await provider.MarkProcessedAsync("saga-a", "event-1", CancellationToken.None);
		await provider.MarkProcessedAsync("saga-b", "event-1", CancellationToken.None);

		// Act & Assert - same key, different sagas
		(await provider.IsProcessedAsync("saga-a", "event-1", CancellationToken.None)).ShouldBeTrue();
		(await provider.IsProcessedAsync("saga-b", "event-1", CancellationToken.None)).ShouldBeTrue();
		(await provider.IsProcessedAsync("saga-c", "event-1", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task MaintainIsolation_BetweenDifferentKeys()
	{
		// Arrange
		var provider = new InMemorySagaIdempotencyProvider();

		await provider.MarkProcessedAsync("saga-1", "event-a", CancellationToken.None);

		// Act & Assert
		(await provider.IsProcessedAsync("saga-1", "event-a", CancellationToken.None)).ShouldBeTrue();
		(await provider.IsProcessedAsync("saga-1", "event-b", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task HandleDuplicateMarks_Idempotently()
	{
		// Arrange
		var provider = new InMemorySagaIdempotencyProvider();

		// Act - mark same key multiple times
		await provider.MarkProcessedAsync("saga-1", "event-1", CancellationToken.None);
		await provider.MarkProcessedAsync("saga-1", "event-1", CancellationToken.None);
		await provider.MarkProcessedAsync("saga-1", "event-1", CancellationToken.None);

		// Assert - should not count duplicates
		provider.Count.ShouldBe(1);
		(await provider.IsProcessedAsync("saga-1", "event-1", CancellationToken.None)).ShouldBeTrue();
	}

	[Fact]
	public async Task ClearResetsAllState()
	{
		// Arrange
		var provider = new InMemorySagaIdempotencyProvider();
		await provider.MarkProcessedAsync("saga-1", "key-1", CancellationToken.None);
		await provider.MarkProcessedAsync("saga-2", "key-2", CancellationToken.None);
		provider.Count.ShouldBe(2);

		// Act
		provider.Clear();

		// Assert
		provider.Count.ShouldBe(0);
		(await provider.IsProcessedAsync("saga-1", "key-1", CancellationToken.None)).ShouldBeFalse();
		(await provider.IsProcessedAsync("saga-2", "key-2", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task RejectCancelledToken()
	{
		// Arrange
		var provider = new InMemorySagaIdempotencyProvider();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => provider.IsProcessedAsync("saga-1", "key-1", cts.Token));
		await Should.ThrowAsync<OperationCanceledException>(
			() => provider.MarkProcessedAsync("saga-1", "key-1", cts.Token));
	}

	[Fact]
	public async Task SimulateSagaEventProcessing_Workflow()
	{
		// Arrange - simulate a saga processing events in order with idempotency
		var provider = new InMemorySagaIdempotencyProvider();
		var sagaId = "order-saga-42";
		var events = new[] { "OrderCreated", "PaymentReceived", "OrderShipped" };

		// Act - process events
		foreach (var eventId in events)
		{
			var alreadyProcessed = await provider.IsProcessedAsync(sagaId, eventId, CancellationToken.None);
			if (!alreadyProcessed)
			{
				// Process event...
				await provider.MarkProcessedAsync(sagaId, eventId, CancellationToken.None);
			}
		}

		// Simulate redelivery of first event
		var isRedelivered = await provider.IsProcessedAsync(sagaId, "OrderCreated", CancellationToken.None);

		// Assert
		isRedelivered.ShouldBeTrue();
		provider.Count.ShouldBe(3);
	}
}
