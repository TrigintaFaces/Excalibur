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
/// Tests for <see cref="IStreamingDocumentHandler{TDocument,TOutput}"/> interface and dispatcher integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamingDocumentHandlerShould
{
	[Fact]
	public async Task ResolveAndInvokeStreamingHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register the streaming handler
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestCsvDocument(["Row1", "Row2", "Row3"]);
		var context = CreateTestContext(provider);

		// Act
		var rows = new List<TestDataRow>();
		await foreach (var row in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
			document, context, CancellationToken.None))
		{
			rows.Add(row);
		}

		// Assert
		rows.Count.ShouldBe(3);
		rows[0].Data.ShouldBe("Row1");
		rows[1].Data.ShouldBe("Row2");
		rows[2].Data.ShouldBe("Row3");
	}

	[Fact]
	public async Task StreamItemsIncrementally()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register the delayed streaming handler
		_ = services.AddScoped<DelayedStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<DelayedStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestCsvDocument(["A", "B", "C"]);
		var context = CreateTestContext(provider);

		// Act - verify items come through incrementally
		var receivedTimestamps = new List<DateTime>();
		await foreach (var row in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
			document, context, CancellationToken.None))
		{
			receivedTimestamps.Add(DateTime.UtcNow);
		}

		// Assert - items should have been received at different times
		receivedTimestamps.Count.ShouldBe(3);
		// The delayed handler adds 10ms between items
		(receivedTimestamps[2] - receivedTimestamps[0]).TotalMilliseconds.ShouldBeGreaterThan(15);
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

		var document = new TestCsvDocument(["Row1"]);
		var context = CreateTestContext(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
				document, context, CancellationToken.None))
			{
				// Should throw before yielding any items
			}
		});

		exception.Message.ShouldContain("IStreamingDocumentHandler");
	}

	[Fact]
	public async Task ThrowWhenDocumentIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register the streaming handler
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = CreateTestContext(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
				null!, context, CancellationToken.None))
			{
				// Should throw before yielding
			}
		});

		exception.ParamName.ShouldBe("document");
	}

	[Fact]
	public async Task ThrowWhenContextIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register the streaming handler
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var document = new TestCsvDocument(["Row1"]);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
				document, null!, CancellationToken.None))
			{
				// Should throw before yielding
			}
		});

		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public async Task PopulateContextPropertiesDuringStreaming()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register the streaming handler
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestCsvDocument(["Row1"]);
		var context = CreateTestContext(provider);

		// Act
		await foreach (var _ in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
			document, context, CancellationToken.None))
		{
			// Consume the stream
		}

		// Assert - context should have been populated
		context.CorrelationId.ShouldNotBeNullOrEmpty();
		context.CausationId.ShouldNotBeNullOrEmpty();
		context.MessageType.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task YieldEmptyStreamForEmptyDocument()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register the streaming handler
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestCsvDocument([]); // Empty document
		var context = CreateTestContext(provider);

		// Act
		var rows = new List<TestDataRow>();
		await foreach (var row in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
			document, context, CancellationToken.None))
		{
			rows.Add(row);
		}

		// Assert
		rows.ShouldBeEmpty();
	}

	[Fact]
	public async Task SupportLargeDocumentStreaming()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register the streaming handler
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Create a document with many rows
		var data = Enumerable.Range(1, 10000).Select(i => $"Row{i}").ToArray();
		var document = new TestCsvDocument(data);
		var context = CreateTestContext(provider);

		// Act
		var count = 0;
		await foreach (var row in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
			document, context, CancellationToken.None))
		{
			count++;
			// Verify data integrity
			row.Data.ShouldStartWith("Row");
		}

		// Assert
		count.ShouldBe(10000);
	}

	private static IMessageContext CreateTestContext(IServiceProvider provider)
	{
		return DispatchContextInitializer.CreateDefaultContext(provider);
	}
}

/// <summary>
/// Test handler that introduces delays to verify incremental streaming.
/// </summary>
file sealed class DelayedStreamingHandler : IStreamingDocumentHandler<TestCsvDocument, TestDataRow>
{
	public async IAsyncEnumerable<TestDataRow> HandleAsync(
		TestCsvDocument document,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		foreach (var row in document.Rows)
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10, cancellationToken).ConfigureAwait(false);
			yield return new TestDataRow(row);
		}
	}
}
