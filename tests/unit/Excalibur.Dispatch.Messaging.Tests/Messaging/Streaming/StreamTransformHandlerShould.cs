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
/// Tests for <see cref="IStreamTransformHandler{TInput,TOutput}"/> interface and dispatcher integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamTransformHandlerShould
{
	[Fact]
	public async Task ResolveAndInvokeTransformHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register transform handler
		_ = services.AddScoped<MappingTransformHandler>();
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestDataRow>>(
			sp => sp.GetRequiredService<MappingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateDocumentStream(5);
		var context = CreateTestContext(provider);

		// Act
		var results = new List<TestDataRow>();
		await foreach (var item in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestDataRow>(
			input, context, CancellationToken.None))
		{
			results.Add(item);
		}

		// Assert
		results.Count.ShouldBe(5);
		results[0].Data.ShouldStartWith("Mapped:");
		results[4].Data.ShouldStartWith("Mapped:");
	}

	[Fact]
	public async Task FilterItemsFromStream()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register filtering handler - only pass even indices
		var filteringHandler = new FilteringTransformHandler
		{
			Predicate = doc => doc.Index % 2 == 0
		};
		_ = services.AddSingleton(filteringHandler);
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestBatchDocument>>(
			sp => sp.GetRequiredService<FilteringTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateIndexedDocumentStream(10);
		var context = CreateTestContext(provider);

		// Act
		var results = new List<TestBatchDocument>();
		await foreach (var item in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestBatchDocument>(
			input, context, CancellationToken.None))
		{
			results.Add(item);
		}

		// Assert - only even indices (0, 2, 4, 6, 8)
		results.Count.ShouldBe(5);
		results.All(r => r.Index % 2 == 0).ShouldBeTrue();
	}

	[Fact]
	public async Task ProcessItemsInOrder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		var trackingHandler = new TrackingTransformHandler();
		_ = services.AddSingleton(trackingHandler);
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestBatchDocument>>(
			sp => sp.GetRequiredService<TrackingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateIndexedDocumentStream(10);
		var context = CreateTestContext(provider);

		// Act
		var results = new List<TestBatchDocument>();
		await foreach (var item in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestBatchDocument>(
			input, context, CancellationToken.None))
		{
			results.Add(item);
		}

		// Assert - order preserved
		results.Count.ShouldBe(10);
		for (var i = 0; i < results.Count; i++)
		{
			results[i].Index.ShouldBe(i);
		}
	}

	[Fact]
	public async Task ThrowWhenNoHandlerRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateDocumentStream(5);
		var context = CreateTestContext(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestDataRow>(
				input, context, CancellationToken.None))
			{
				// Should throw before yielding
			}
		});

		exception.Message.ShouldContain("IStreamTransformHandler");
	}

	[Fact]
	public async Task ThrowWhenInputIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<MappingTransformHandler>();
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestDataRow>>(
			sp => sp.GetRequiredService<MappingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = CreateTestContext(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestDataRow>(
				null!, context, CancellationToken.None))
			{
				// Should throw before yielding
			}
		});

		exception.ParamName.ShouldBe("input");
	}

	[Fact]
	public async Task ThrowWhenContextIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<MappingTransformHandler>();
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestDataRow>>(
			sp => sp.GetRequiredService<MappingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var input = CreateDocumentStream(5);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestDataRow>(
				input, null!, CancellationToken.None))
			{
				// Should throw before yielding
			}
		});

		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public async Task CancelTransformWhenTokenCancelled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		var slowHandler = new SlowTransformHandler { DelayMs = 50 };
		_ = services.AddSingleton(slowHandler);
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestBatchDocument>>(
			sp => sp.GetRequiredService<SlowTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateDocumentStream(10);
		var context = CreateTestContext(provider);

		using var cts = new CancellationTokenSource();
		var itemsReceived = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestBatchDocument>(
				input, context, cts.Token))
			{
				itemsReceived++;
				if (itemsReceived == 2)
				{
					await cts.CancelAsync();
				}
			}
		});

		itemsReceived.ShouldBe(2);
	}

	[Fact]
	public async Task PropagateErrorsFromTransformHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		var errorHandler = new ErrorThrowingTransformHandler { ThrowAfterItems = 3 };
		_ = services.AddSingleton(errorHandler);
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestBatchDocument>>(
			sp => sp.GetRequiredService<ErrorThrowingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateDocumentStream(10);
		var context = CreateTestContext(provider);
		var itemsReceived = 0;

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestBatchDocument>(
				input, context, CancellationToken.None))
			{
				itemsReceived++;
			}
		});

		exception.Message.ShouldContain("Simulated transform error");
		itemsReceived.ShouldBe(3);
	}

	[Fact]
	public async Task HandleEmptyInputStream()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<MappingTransformHandler>();
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestDataRow>>(
			sp => sp.GetRequiredService<MappingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateDocumentStream(0);
		var context = CreateTestContext(provider);

		// Act
		var results = new List<TestDataRow>();
		await foreach (var item in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestDataRow>(
			input, context, CancellationToken.None))
		{
			results.Add(item);
		}

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task SupportLargeStreamTransformation()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<MappingTransformHandler>();
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestDataRow>>(
			sp => sp.GetRequiredService<MappingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateDocumentStream(10000);
		var context = CreateTestContext(provider);

		// Act
		var count = 0;
		await foreach (var item in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestDataRow>(
			input, context, CancellationToken.None))
		{
			count++;
			item.Data.ShouldStartWith("Mapped:");
		}

		// Assert
		count.ShouldBe(10000);
	}

	[Fact]
	public async Task PopulateContextPropertiesDuringTransform()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<MappingTransformHandler>();
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestDataRow>>(
			sp => sp.GetRequiredService<MappingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateDocumentStream(1);
		var context = CreateTestContext(provider);

		// Act
		await foreach (var _ in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestDataRow>(
			input, context, CancellationToken.None))
		{
			// Consume
		}

		// Assert
		context.CorrelationId.ShouldNotBeNullOrEmpty();
		context.CausationId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task RespectBackpressureFromSlowConsumer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		var trackingHandler = new TrackingTransformHandler();
		_ = services.AddSingleton(trackingHandler);
		_ = services.AddScoped<IStreamTransformHandler<TestBatchDocument, TestBatchDocument>>(
			sp => sp.GetRequiredService<TrackingTransformHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var input = CreateDocumentStream(10);
		var context = CreateTestContext(provider);

		var timestamps = new List<DateTime>();

		// Act - slow consumer (30ms per item)
		await foreach (var _ in dispatcher.DispatchTransformStreamAsync<TestBatchDocument, TestBatchDocument>(
			input, context, CancellationToken.None))
		{
			timestamps.Add(DateTime.UtcNow);
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(30);
		}

		// Assert - should take at least 270ms for 10 items with 30ms delay each
		timestamps.Count.ShouldBe(10);
		var totalElapsed = timestamps[9] - timestamps[0];
		totalElapsed.TotalMilliseconds.ShouldBeGreaterThan(200);
	}

	#region Helper Methods

	private static async IAsyncEnumerable<TestBatchDocument> CreateDocumentStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		for (var i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return new TestBatchDocument($"Item{i}");
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	private static async IAsyncEnumerable<TestBatchDocument> CreateIndexedDocumentStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		for (var i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return new TestBatchDocument($"Item{i}") { Index = i };
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	private static IMessageContext CreateTestContext(IServiceProvider provider)
	{
		return DispatchContextInitializer.CreateDefaultContext(provider);
	}

	#endregion
}
