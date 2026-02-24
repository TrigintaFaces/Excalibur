// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Streaming.TestTypes;

#region Document Types

/// <summary>
/// Test document representing a CSV file for streaming tests.
/// </summary>
public sealed class TestCsvDocument : IDispatchDocument
{
	public TestCsvDocument(string[] rows) => Rows = rows;

	public string[] Rows { get; }

	public Guid Id { get; } = Guid.NewGuid();
	public string MessageId { get; } = Guid.NewGuid().ToString();
	public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
	public MessageKinds Kind => MessageKinds.Document;
	public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
	public object Body => Rows;
	public string MessageType { get; } = nameof(TestCsvDocument);
	public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
}

/// <summary>
/// Test document for stream consumption tests.
/// </summary>
public sealed class TestBatchDocument : IDispatchDocument
{
	public TestBatchDocument(string data) => Data = data;

	public string Data { get; }
	public int Index { get; init; }

	public Guid Id { get; } = Guid.NewGuid();
	public string MessageId { get; } = Guid.NewGuid().ToString();
	public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
	public MessageKinds Kind => MessageKinds.Document;
	public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
	public object Body => Data;
	public string MessageType { get; } = nameof(TestBatchDocument);
	public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
}

#endregion

#region Output Types

/// <summary>
/// Test data row output from streaming handlers.
/// </summary>
public sealed class TestDataRow
{
	public TestDataRow(string data) => Data = data;

	public string Data { get; }
	public DateTimeOffset ProcessedAt { get; } = DateTimeOffset.UtcNow;
}

#endregion

#region Streaming Handlers

/// <summary>
/// Test streaming handler that converts CSV document rows to data rows.
/// </summary>
public sealed class TestCsvStreamingHandler : IStreamingDocumentHandler<TestCsvDocument, TestDataRow>
{
	public async IAsyncEnumerable<TestDataRow> HandleAsync(
		TestCsvDocument document,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		foreach (var row in document.Rows)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return new TestDataRow(row);
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}
}

/// <summary>
/// Streaming handler that throws an exception mid-stream.
/// </summary>
public sealed class ErrorThrowingStreamingHandler : IStreamingDocumentHandler<TestCsvDocument, TestDataRow>
{
	public int ThrowAfterItems { get; init; } = 2;

	public async IAsyncEnumerable<TestDataRow> HandleAsync(
		TestCsvDocument document,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var count = 0;
		foreach (var row in document.Rows)
		{
			if (count >= ThrowAfterItems)
			{
				throw new InvalidOperationException("Simulated streaming error");
			}

			yield return new TestDataRow(row);
			count++;
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}
}

/// <summary>
/// Streaming handler that respects cancellation properly.
/// </summary>
public sealed class CancellationAwareStreamingHandler : IStreamingDocumentHandler<TestCsvDocument, TestDataRow>
{
	public async IAsyncEnumerable<TestDataRow> HandleAsync(
		TestCsvDocument document,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		foreach (var row in document.Rows)
		{
			// Delay to allow cancellation to be observed
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			yield return new TestDataRow(row);
		}
	}
}

#endregion

#region Stream Consumer Handlers

/// <summary>
/// Test stream consumer handler that collects all documents into a list.
/// </summary>
public sealed class CollectingStreamConsumerHandler : IStreamConsumerHandler<TestBatchDocument>
{
	private readonly List<TestBatchDocument> _collected = [];

	public IReadOnlyList<TestBatchDocument> Collected => _collected;

	public async Task HandleAsync(
		IAsyncEnumerable<TestBatchDocument> documents,
		CancellationToken cancellationToken)
	{
		await foreach (var doc in documents.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			_collected.Add(doc);
		}
	}
}

/// <summary>
/// Test stream consumer handler that processes documents in batches.
/// </summary>
public sealed class BatchingStreamConsumerHandler : IStreamConsumerHandler<TestBatchDocument>
{
	private readonly List<List<TestBatchDocument>> _batches = [];

	public int BatchSize { get; init; } = 10;
	public IReadOnlyList<List<TestBatchDocument>> Batches => _batches;
	public int TotalProcessed { get; private set; }

	public async Task HandleAsync(
		IAsyncEnumerable<TestBatchDocument> documents,
		CancellationToken cancellationToken)
	{
		var batch = new List<TestBatchDocument>();

		await foreach (var doc in documents.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			batch.Add(doc);
			TotalProcessed++;

			if (batch.Count >= BatchSize)
			{
				_batches.Add([.. batch]);
				batch.Clear();
			}
		}

		// Add any remaining items
		if (batch.Count > 0)
		{
			_batches.Add(batch);
		}
	}
}

/// <summary>
/// Test stream consumer handler that applies backpressure by processing slowly.
/// </summary>
public sealed class SlowStreamConsumerHandler : IStreamConsumerHandler<TestBatchDocument>
{
	public int ProcessedCount { get; private set; }
	public int DelayMs { get; init; } = 50;

	public async Task HandleAsync(
		IAsyncEnumerable<TestBatchDocument> documents,
		CancellationToken cancellationToken)
	{
		await foreach (var doc in documents.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(DelayMs, cancellationToken).ConfigureAwait(false);
			ProcessedCount++;
		}
	}
}

/// <summary>
/// Test stream consumer handler that throws an error after processing some items.
/// </summary>
public sealed class ErrorThrowingStreamConsumerHandler : IStreamConsumerHandler<TestBatchDocument>
{
	public int ThrowAfterItems { get; init; } = 5;
	public int ProcessedCount { get; private set; }

	public async Task HandleAsync(
		IAsyncEnumerable<TestBatchDocument> documents,
		CancellationToken cancellationToken)
	{
		await foreach (var doc in documents.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			ProcessedCount++;

			if (ProcessedCount >= ThrowAfterItems)
			{
				throw new InvalidOperationException("Simulated consumer error");
			}
		}
	}
}

#endregion

#region Stream Transform Handlers

/// <summary>
/// Test transform handler that filters documents based on a predicate.
/// </summary>
public sealed class FilteringTransformHandler : IStreamTransformHandler<TestBatchDocument, TestBatchDocument>
{
	public Func<TestBatchDocument, bool> Predicate { get; init; } = _ => true;

	public async IAsyncEnumerable<TestBatchDocument> HandleAsync(
		IAsyncEnumerable<TestBatchDocument> input,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var doc in input.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			if (Predicate(doc))
			{
				yield return doc;
			}
		}
	}
}

/// <summary>
/// Test transform handler that maps documents to a different type.
/// </summary>
public sealed class MappingTransformHandler : IStreamTransformHandler<TestBatchDocument, TestDataRow>
{
	public async IAsyncEnumerable<TestDataRow> HandleAsync(
		IAsyncEnumerable<TestBatchDocument> input,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var doc in input.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			yield return new TestDataRow($"Mapped:{doc.Data}");
		}
	}
}

/// <summary>
/// Test transform handler that throws an error mid-stream.
/// </summary>
public sealed class ErrorThrowingTransformHandler : IStreamTransformHandler<TestBatchDocument, TestBatchDocument>
{
	public int ThrowAfterItems { get; init; } = 3;

	public async IAsyncEnumerable<TestBatchDocument> HandleAsync(
		IAsyncEnumerable<TestBatchDocument> input,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var count = 0;
		await foreach (var doc in input.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			if (count >= ThrowAfterItems)
			{
				throw new InvalidOperationException("Simulated transform error");
			}

			yield return doc;
			count++;
		}
	}
}

/// <summary>
/// Test transform handler that tracks processing for verification.
/// </summary>
public sealed class TrackingTransformHandler : IStreamTransformHandler<TestBatchDocument, TestBatchDocument>
{
	private readonly List<string> _processedItems = [];

	public IReadOnlyList<string> ProcessedItems => _processedItems;

	public async IAsyncEnumerable<TestBatchDocument> HandleAsync(
		IAsyncEnumerable<TestBatchDocument> input,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var doc in input.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			_processedItems.Add(doc.Data);
			yield return doc;
		}
	}
}

/// <summary>
/// Test transform handler with delay for backpressure testing.
/// </summary>
public sealed class SlowTransformHandler : IStreamTransformHandler<TestBatchDocument, TestBatchDocument>
{
	public int DelayMs { get; init; } = 10;

	public async IAsyncEnumerable<TestBatchDocument> HandleAsync(
		IAsyncEnumerable<TestBatchDocument> input,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var doc in input.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(DelayMs, cancellationToken).ConfigureAwait(false);
			yield return doc;
		}
	}
}

#endregion

#region Progress Document Handlers

/// <summary>
/// Test document for progress handler tests.
/// </summary>
public sealed class TestProgressDocument : IDispatchDocument
{
	public TestProgressDocument(int itemCount) => ItemCount = itemCount;

	public int ItemCount { get; }

	public Guid Id { get; } = Guid.NewGuid();
	public string MessageId { get; } = Guid.NewGuid().ToString();
	public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
	public MessageKinds Kind => MessageKinds.Document;
	public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
	public object Body => ItemCount;
	public string MessageType { get; } = nameof(TestProgressDocument);
	public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
}

/// <summary>
/// Test progress handler that reports progress at regular intervals.
/// </summary>
public sealed class TestProgressHandler : IProgressDocumentHandler<TestProgressDocument>
{
	public int ProcessingDelayMs { get; init; } = 1;

	public async Task HandleAsync(
		TestProgressDocument document,
		IProgress<DocumentProgress> progress,
		CancellationToken cancellationToken)
	{
		for (var i = 0; i < document.ItemCount; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(ProcessingDelayMs, cancellationToken).ConfigureAwait(false);

			progress.Report(DocumentProgress.FromItems(
				itemsProcessed: i + 1,
				totalItems: document.ItemCount,
				currentPhase: $"Processing item {i + 1}"));
		}

		progress.Report(DocumentProgress.Completed(document.ItemCount, "Processing complete"));
	}
}

/// <summary>
/// Test progress handler that throws an error mid-processing.
/// </summary>
public sealed class ErrorThrowingProgressHandler : IProgressDocumentHandler<TestProgressDocument>
{
	public int ThrowAfterItems { get; init; } = 5;

	public async Task HandleAsync(
		TestProgressDocument document,
		IProgress<DocumentProgress> progress,
		CancellationToken cancellationToken)
	{
		for (var i = 0; i < document.ItemCount; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (i >= ThrowAfterItems)
			{
				throw new InvalidOperationException("Simulated progress handler error");
			}

			progress.Report(DocumentProgress.FromItems(
				itemsProcessed: i + 1,
				totalItems: document.ItemCount));

			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1, cancellationToken).ConfigureAwait(false);
		}
	}
}

/// <summary>
/// Test progress handler that reports indeterminate progress.
/// </summary>
public sealed class IndeterminateProgressHandler : IProgressDocumentHandler<TestProgressDocument>
{
	public async Task HandleAsync(
		TestProgressDocument document,
		IProgress<DocumentProgress> progress,
		CancellationToken cancellationToken)
	{
		for (var i = 0; i < document.ItemCount; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			progress.Report(DocumentProgress.Indeterminate(
				itemsProcessed: i + 1,
				currentPhase: "Processing..."));

			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1, cancellationToken).ConfigureAwait(false);
		}

		progress.Report(DocumentProgress.Completed(document.ItemCount));
	}
}

/// <summary>
/// Test progress handler with multi-phase processing.
/// </summary>
public sealed class MultiPhaseProgressHandler : IProgressDocumentHandler<TestProgressDocument>
{
	private static readonly string[] Phases = ["Initializing", "Processing", "Finalizing"];

	public async Task HandleAsync(
		TestProgressDocument document,
		IProgress<DocumentProgress> progress,
		CancellationToken cancellationToken)
	{
		var itemsPerPhase = document.ItemCount / Phases.Length;
		var totalProcessed = 0L;

		foreach (var phase in Phases)
		{
			for (var i = 0; i < itemsPerPhase; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				totalProcessed++;
				progress.Report(DocumentProgress.FromItems(
					itemsProcessed: totalProcessed,
					totalItems: document.ItemCount,
					currentPhase: phase));

				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1, cancellationToken).ConfigureAwait(false);
			}
		}

		progress.Report(DocumentProgress.Completed(totalProcessed, "All phases complete"));
	}
}

#endregion
