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
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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
		var dispatcher = provider.GetRequiredService<IStreamingDispatcher>();

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

		// Gate: handler blocks after yielding item 1 until the test signals it
		var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Register the gated streaming handler
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			_ => new GatedStreamingHandler(gate));

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IStreamingDispatcher>();

		var document = new TestCsvDocument(["A", "B", "C"]);
		var context = CreateTestContext(provider);

		// Act - verify the first item is observable before the stream completes
		var firstItemReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var receivedRows = new List<TestDataRow>();
		var receiveTask = Task.Run(async () =>
		{
			await foreach (var row in dispatcher.DispatchStreamingAsync<TestCsvDocument, TestDataRow>(
				document, context, CancellationToken.None))
			{
				lock (receivedRows)
				{
					receivedRows.Add(row);
					if (receivedRows.Count == 1)
					{
						firstItemReceived.TrySetResult();
					}
				}
			}
		});

		await firstItemReceived.Task.WaitAsync(TimeSpan.FromSeconds(30));

		// Assert - exactly 1 item received because the handler is blocked on the gate
		lock (receivedRows)
		{
			receivedRows.Count.ShouldBe(1);
		}

		receiveTask.IsCompleted.ShouldBeFalse();

		// Release the gate so the handler yields the remaining items
		gate.TrySetResult();

		await receiveTask.WaitAsync(TimeSpan.FromSeconds(30));

		lock (receivedRows)
		{
			receivedRows.Count.ShouldBe(3);
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
		var dispatcher = provider.GetRequiredService<IStreamingDispatcher>();

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
		var dispatcher = provider.GetRequiredService<IStreamingDispatcher>();
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
		var dispatcher = provider.GetRequiredService<IStreamingDispatcher>();
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
		var dispatcher = provider.GetRequiredService<IStreamingDispatcher>();

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
		context.GetMessageType().ShouldNotBeNullOrEmpty();
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
		var dispatcher = provider.GetRequiredService<IStreamingDispatcher>();

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
		var dispatcher = provider.GetRequiredService<IStreamingDispatcher>();

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
/// Test handler that uses an explicit gate to verify incremental streaming.
/// Yields the first item immediately, then blocks on a <see cref="TaskCompletionSource"/>
/// gate before yielding subsequent items. This eliminates timing-dependent races.
/// </summary>
file sealed class GatedStreamingHandler : IStreamingDocumentHandler<TestCsvDocument, TestDataRow>
{
	private readonly TaskCompletionSource _gate;

	public GatedStreamingHandler(TaskCompletionSource gate)
	{
		_gate = gate;
	}

	public async IAsyncEnumerable<TestDataRow> HandleAsync(
		TestCsvDocument document,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var first = true;
		foreach (var row in document.Rows)
		{
			if (first)
			{
				first = false;
			}
			else
			{
				// Block until the test signals the gate
				await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}

			yield return new TestDataRow(row);
		}
	}
}
