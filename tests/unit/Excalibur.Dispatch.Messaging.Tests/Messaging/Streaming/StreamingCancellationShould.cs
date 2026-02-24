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
/// Tests for cancellation behavior in streaming handlers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamingCancellationShould
{
	#region DispatchStreamingAsync Cancellation Tests

	[Fact]
	public async Task CancelStreamingHandlerWhenTokenCancelled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register cancellation-aware streaming handler
		_ = services.AddScoped<CancellationAwareStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<CancellationAwareStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestCsvDocument(["A", "B", "C", "D", "E"]);
		var context = CreateTestContext(provider);

		using var cts = new CancellationTokenSource();
		var itemsReceived = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await foreach (var row in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
				document, context, cts.Token))
			{
				itemsReceived++;
				if (itemsReceived == 2)
				{
					// Cancel after receiving 2 items
					await cts.CancelAsync();
				}
			}
		});

		// Only 2 items should have been received before cancellation
		itemsReceived.ShouldBe(2);
	}

	[Fact]
	public async Task RespectPrecancelledToken()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register streaming handler
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestCsvDocument(["A", "B", "C"]);
		var context = CreateTestContext(provider);

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var itemsReceived = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
				document, context, cts.Token))
			{
				itemsReceived++;
			}
		});

		// No items should have been received
		itemsReceived.ShouldBe(0);
	}

	[Fact]
	public async Task PropagateErrorsFromStreamingHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var handler = new ErrorThrowingStreamingHandler { ThrowAfterItems = 2 };
		_ = services.AddSingleton(handler);
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(sp =>
			sp.GetRequiredService<ErrorThrowingStreamingHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestCsvDocument(["A", "B", "C", "D"]);
		var context = CreateTestContext(provider);

		var itemsReceived = 0;

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
				document, context, CancellationToken.None))
			{
				itemsReceived++;
			}
		});

		exception.Message.ShouldContain("Simulated streaming error");
		itemsReceived.ShouldBe(2); // Should have received 2 items before error
	}

	#endregion

	#region DispatchStreamAsync Cancellation Tests

	[Fact]
	public async Task CancelStreamConsumerWhenTokenCancelled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var slowConsumer = new SlowStreamConsumerHandler { DelayMs = 50 };
		_ = services.AddSingleton(slowConsumer);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp =>
			sp.GetRequiredService<SlowStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		using var cts = new CancellationTokenSource();

		// Create a stream that produces many items
		var documents = CreateSlowDocumentStream(10, cts.Token);
		var context = CreateTestContext(provider);

		// Cancel after a short delay
		_ = global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100).ContinueWith(_ => cts.Cancel());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await dispatcher.DispatchStreamAsync(documents, context, cts.Token);
		});

		// Should have processed some items but not all
		slowConsumer.ProcessedCount.ShouldBeLessThan(10);
	}

	[Fact]
	public async Task RespectPrecancelledTokenForStreamConsumer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var collector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(collector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp =>
			sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var documents = CreateTestDocumentStream(5, cts.Token);
		var context = CreateTestContext(provider);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await dispatcher.DispatchStreamAsync(documents, context, cts.Token);
		});

		collector.Collected.Count.ShouldBe(0);
	}

	[Fact]
	public async Task PropagateErrorsFromStreamConsumer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var errorHandler = new ErrorThrowingStreamConsumerHandler { ThrowAfterItems = 3 };
		_ = services.AddSingleton(errorHandler);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp =>
			sp.GetRequiredService<ErrorThrowingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var documents = CreateTestDocumentStream(10, CancellationToken.None);
		var context = CreateTestContext(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);
		});

		exception.Message.ShouldContain("Simulated consumer error");
		errorHandler.ProcessedCount.ShouldBe(3);
	}

	#endregion

	#region Helper Methods

	private static async IAsyncEnumerable<TestBatchDocument> CreateTestDocumentStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		for (var i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return new TestBatchDocument($"Item{i}");
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	private static async IAsyncEnumerable<TestBatchDocument> CreateSlowDocumentStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		for (var i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(20, cancellationToken).ConfigureAwait(false);
			yield return new TestBatchDocument($"Item{i}");
		}
	}

	private static IMessageContext CreateTestContext(IServiceProvider provider)
	{
		return DispatchContextInitializer.CreateDefaultContext(provider);
	}

	#endregion
}
