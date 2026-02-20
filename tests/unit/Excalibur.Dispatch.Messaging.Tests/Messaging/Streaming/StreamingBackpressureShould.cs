// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Tests.Messaging.Streaming.TestTypes;

namespace Excalibur.Dispatch.Tests.Messaging.Streaming;

/// <summary>
/// Tests for backpressure behavior in streaming handlers.
/// Verifies that slow consumers naturally throttle fast producers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamingBackpressureShould
{
	[Fact]
	public async Task AllowSlowConsumerToControlRate()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var slowConsumer = new SlowStreamConsumerHandler { DelayMs = 20 };
		_ = services.AddSingleton(slowConsumer);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp =>
			sp.GetRequiredService<SlowStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var startTime = DateTime.UtcNow;

		// Create a fast producer stream
		var documents = CreateFastDocumentStream(10);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		var elapsed = DateTime.UtcNow - startTime;

		// Assert - slow consumer should take at least 200ms (10 items * 20ms delay)
		slowConsumer.ProcessedCount.ShouldBe(10);
		elapsed.TotalMilliseconds.ShouldBeGreaterThan(150); // Allow some tolerance
	}

	[Fact]
	public async Task NotBufferEntireStreamInMemory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var trackingConsumer = new MemoryTrackingStreamConsumerHandler();
		_ = services.AddSingleton(trackingConsumer);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp =>
			sp.GetRequiredService<MemoryTrackingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Create a large stream
		var documents = CreateLargeDocumentStream(1000);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		// Assert
		trackingConsumer.ProcessedCount.ShouldBe(1000);
		// The max concurrent items should be low (ideally 1) since we process one at a time
		trackingConsumer.MaxConcurrentItems.ShouldBeLessThan(10);
	}

	[Fact]
	public async Task AllowSlowStreamingProducerWithFastConsumer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var fastCollector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(fastCollector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp =>
			sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var startTime = DateTime.UtcNow;

		// Create a slow producer stream (each item takes 10ms to produce)
		var documents = CreateSlowProducerStream(10);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		var elapsed = DateTime.UtcNow - startTime;

		// Assert - should take at least 100ms (10 items * 10ms delay from producer)
		fastCollector.Collected.Count.ShouldBe(10);
		elapsed.TotalMilliseconds.ShouldBeGreaterThan(80); // Allow some tolerance
	}

	[Fact]
	public async Task ProcessStreamingOutputIncrementallyWithBackpressure()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register streaming handler
		_ = services.AddScoped<SlowYieldingStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<SlowYieldingStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestCsvDocument(["A", "B", "C", "D", "E"]);
		var context = CreateTestContext(provider);

		var processedTimestamps = new List<DateTime>();

		// Act - simulate a slow consumer
		await foreach (var row in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
			document, context, CancellationToken.None))
		{
			processedTimestamps.Add(DateTime.UtcNow);
			await Task.Delay(30); // Simulate slow processing by consumer
		}

		// Assert - items should be received incrementally, not all at once
		processedTimestamps.Count.ShouldBe(5);

		// Total elapsed time should be at least 150ms (5 items * 30ms consumer delay)
		var totalElapsed = processedTimestamps[4] - processedTimestamps[0];
		totalElapsed.TotalMilliseconds.ShouldBeGreaterThan(100);
	}

	#region Helper Types

	private sealed class MemoryTrackingStreamConsumerHandler : IStreamConsumerHandler<TestBatchDocument>
	{
		private int _currentItems;

		public int ProcessedCount { get; private set; }
		public int MaxConcurrentItems { get; private set; }

		public async Task HandleAsync(
			IAsyncEnumerable<TestBatchDocument> documents,
			CancellationToken cancellationToken)
		{
			await foreach (var doc in documents.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				_ = Interlocked.Increment(ref _currentItems);
				var current = _currentItems;
				if (current > MaxConcurrentItems)
				{
					MaxConcurrentItems = current;
				}

				// Simulate some work
				await Task.Delay(1, cancellationToken).ConfigureAwait(false);

				_ = Interlocked.Decrement(ref _currentItems);
				ProcessedCount++;
			}
		}
	}

	private sealed class SlowYieldingStreamingHandler : IStreamingDocumentHandler<TestCsvDocument, TestDataRow>
	{
		public async IAsyncEnumerable<TestDataRow> HandleAsync(
			TestCsvDocument document,
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			foreach (var row in document.Rows)
			{
				cancellationToken.ThrowIfCancellationRequested();
				// Producer is fast but respects backpressure through yield
				yield return new TestDataRow(row);
			}

			await Task.CompletedTask.ConfigureAwait(false);
		}
	}

	#endregion

	#region Helper Methods

	private static async IAsyncEnumerable<TestBatchDocument> CreateFastDocumentStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		// Fast producer - no delays
		for (var i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return new TestBatchDocument($"Fast{i}");
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	private static async IAsyncEnumerable<TestBatchDocument> CreateSlowProducerStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		// Slow producer - 10ms delay per item
		for (var i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Delay(10, cancellationToken).ConfigureAwait(false);
			yield return new TestBatchDocument($"Slow{i}");
		}
	}

	private static async IAsyncEnumerable<TestBatchDocument> CreateLargeDocumentStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		for (var i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return new TestBatchDocument($"Large{i}");
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	private static IMessageContext CreateTestContext(IServiceProvider provider)
	{
		return DispatchContextInitializer.CreateDefaultContext(provider);
	}

	#endregion
}
