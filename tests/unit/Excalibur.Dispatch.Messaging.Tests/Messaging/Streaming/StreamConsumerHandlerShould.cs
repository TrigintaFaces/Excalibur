// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Tests.Messaging.Streaming.TestTypes;

namespace Excalibur.Dispatch.Tests.Messaging.Streaming;

/// <summary>
/// Tests for <see cref="IStreamConsumerHandler{TDocument}"/> interface and dispatcher integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamConsumerHandlerShould
{
	[Fact]
	public async Task ResolveAndInvokeStreamConsumerHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var collector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(collector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var documents = CreateTestDocumentStream(5);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		// Assert
		collector.Collected.Count.ShouldBe(5);
		collector.Collected[0].Data.ShouldBe("Item0");
		collector.Collected[4].Data.ShouldBe("Item4");
	}

	[Fact]
	public async Task ProcessDocumentsInOrder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var collector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(collector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var documents = CreateIndexedDocumentStream(10);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		// Assert - verify order is preserved
		for (var i = 0; i < collector.Collected.Count; i++)
		{
			collector.Collected[i].Index.ShouldBe(i);
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

		var documents = CreateTestDocumentStream(3);
		var context = CreateTestContext(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);
		});

		exception.Message.ShouldContain("IStreamConsumerHandler");
	}

	[Fact]
	public async Task ThrowWhenDocumentsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var collector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(collector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = CreateTestContext(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await dispatcher.DispatchStreamAsync<TestBatchDocument>(null!, context, CancellationToken.None);
		});

		exception.ParamName.ShouldBe("documents");
	}

	[Fact]
	public async Task ThrowWhenContextIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var collector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(collector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var documents = CreateTestDocumentStream(3);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await dispatcher.DispatchStreamAsync(documents, null!, CancellationToken.None);
		});

		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public async Task PopulateContextPropertiesDuringConsumption()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var collector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(collector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var documents = CreateTestDocumentStream(1);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		// Assert - context should have been populated
		context.CorrelationId.ShouldNotBeNullOrEmpty();
		context.CausationId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task HandleEmptyStream()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var collector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(collector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var documents = CreateTestDocumentStream(0);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		// Assert
		collector.Collected.ShouldBeEmpty();
	}

	[Fact]
	public async Task SupportLargeStreamConsumption()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var collector = new CollectingStreamConsumerHandler();
		_ = services.AddSingleton(collector);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var documents = CreateTestDocumentStream(10000);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		// Assert
		collector.Collected.Count.ShouldBe(10000);
	}

	[Fact]
	public async Task SupportBatchingConsumer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var batcher = new BatchingStreamConsumerHandler { BatchSize = 25 };
		_ = services.AddSingleton(batcher);
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(sp => sp.GetRequiredService<BatchingStreamConsumerHandler>());

		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var documents = CreateTestDocumentStream(100);
		var context = CreateTestContext(provider);

		// Act
		await dispatcher.DispatchStreamAsync(documents, context, CancellationToken.None);

		// Assert
		batcher.TotalProcessed.ShouldBe(100);
		batcher.Batches.Count.ShouldBe(4); // 100 / 25 = 4 batches
		batcher.Batches[0].Count.ShouldBe(25);
		batcher.Batches[3].Count.ShouldBe(25);
	}

	private static async IAsyncEnumerable<TestBatchDocument> CreateTestDocumentStream(
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
}
