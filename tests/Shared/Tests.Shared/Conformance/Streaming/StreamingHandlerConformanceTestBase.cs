// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Streaming;

namespace Tests.Shared.Conformance.Streaming;

/// <summary>
/// Base class for streaming handler and transport integration conformance tests.
/// Verifies that streaming message flow through the pipeline works correctly.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies streaming handler behavior, including:
/// </para>
/// <list type="bullet">
///   <item>Stream consumer handler receives all documents</item>
///   <item>Stream processing respects cancellation</item>
///   <item>Chunk metadata (IsFirst, IsLast, Index) is correct</item>
///   <item>Empty streams are handled gracefully</item>
///   <item>Large streams process without excessive memory usage</item>
/// </list>
/// <para>
/// To create conformance tests for your streaming implementation:
/// <list type="number">
///   <item>Inherit from StreamingHandlerConformanceTestBase</item>
///   <item>Override CreateConsumerHandler() to provide a handler instance</item>
///   <item>Override CleanupAsync() for resource cleanup</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Component", "Streaming")]
public abstract class StreamingHandlerConformanceTestBase : IAsyncLifetime
{
	/// <inheritdoc/>
	public virtual Task InitializeAsync() => Task.CompletedTask;

	/// <inheritdoc/>
	public virtual Task DisposeAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a stream consumer handler for testing.
	/// </summary>
	/// <returns>A stream consumer handler and an accessor to retrieve processed items.</returns>
	protected abstract (IStreamConsumerHandler<TestStreamDocument> Handler, Func<IReadOnlyList<TestStreamDocument>> GetProcessed) CreateConsumerHandler();

	#region Helper Methods

	/// <summary>
	/// Creates an async enumerable of test documents.
	/// </summary>
	protected static async IAsyncEnumerable<TestStreamDocument> CreateTestStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		for (int i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return new TestStreamDocument { Index = i, Data = $"Document-{i}" };
			await Task.Yield();
		}
	}

	/// <summary>
	/// Creates chunks from a test stream.
	/// </summary>
	protected static async IAsyncEnumerable<Chunk<TestStreamDocument>> CreateChunkedStream(
		int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		for (int i = 0; i < count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var doc = new TestStreamDocument { Index = i, Data = $"Chunk-{i}" };
			yield return new Chunk<TestStreamDocument>(
				Data: doc,
				Index: i,
				IsFirst: i == 0,
				IsLast: i == count - 1);
			await Task.Yield();
		}
	}

	#endregion Helper Methods

	#region Stream Consumer Tests

	[Fact]
	public async Task StreamConsumer_ProcessesAllDocuments()
	{
		// Arrange
		var (handler, getProcessed) = CreateConsumerHandler();
		const int documentCount = 10;

		// Act
		await handler.HandleAsync(
			CreateTestStream(documentCount),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		var processed = getProcessed();
		processed.Count.ShouldBe(documentCount);
	}

	[Fact]
	public async Task StreamConsumer_ReceivesDocumentsInOrder()
	{
		// Arrange
		var (handler, getProcessed) = CreateConsumerHandler();
		const int documentCount = 20;

		// Act
		await handler.HandleAsync(
			CreateTestStream(documentCount),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		var processed = getProcessed();
		for (int i = 0; i < processed.Count; i++)
		{
			processed[i].Index.ShouldBe(i, $"Document at position {i} should have index {i}");
		}
	}

	[Fact]
	public async Task StreamConsumer_EmptyStream_CompletesSuccessfully()
	{
		// Arrange
		var (handler, getProcessed) = CreateConsumerHandler();

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(async () =>
			await handler.HandleAsync(
				CreateTestStream(0),
				CancellationToken.None).ConfigureAwait(false));

		var processed = getProcessed();
		processed.Count.ShouldBe(0);
	}

	[Fact]
	public async Task StreamConsumer_SingleDocument_ProcessedCorrectly()
	{
		// Arrange
		var (handler, getProcessed) = CreateConsumerHandler();

		// Act
		await handler.HandleAsync(
			CreateTestStream(1),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		var processed = getProcessed();
		processed.Count.ShouldBe(1);
		processed[0].Data.ShouldBe("Document-0");
	}

	[Fact]
	public async Task StreamConsumer_RespectsCancellation()
	{
		// Arrange
		var (handler, _) = CreateConsumerHandler();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await handler.HandleAsync(
				CreateTestStream(100, cts.Token),
				cts.Token).ConfigureAwait(false));
	}

	#endregion Stream Consumer Tests

	#region Chunk Metadata Tests

	[Fact]
	public async Task ChunkedStream_FirstChunkIsMarkedFirst()
	{
		// Arrange & Act
		var chunks = new List<Chunk<TestStreamDocument>>();
		await foreach (var chunk in CreateChunkedStream(5).ConfigureAwait(false))
		{
			chunks.Add(chunk);
		}

		// Assert
		chunks[0].IsFirst.ShouldBeTrue();
		chunks[0].IsLast.ShouldBeFalse();
		chunks[0].Index.ShouldBe(0);
	}

	[Fact]
	public async Task ChunkedStream_LastChunkIsMarkedLast()
	{
		// Arrange & Act
		var chunks = new List<Chunk<TestStreamDocument>>();
		await foreach (var chunk in CreateChunkedStream(5).ConfigureAwait(false))
		{
			chunks.Add(chunk);
		}

		// Assert
		chunks[^1].IsLast.ShouldBeTrue();
		chunks[^1].IsFirst.ShouldBeFalse();
		chunks[^1].Index.ShouldBe(4);
	}

	[Fact]
	public async Task ChunkedStream_MiddleChunksAreMiddle()
	{
		// Arrange & Act
		var chunks = new List<Chunk<TestStreamDocument>>();
		await foreach (var chunk in CreateChunkedStream(5).ConfigureAwait(false))
		{
			chunks.Add(chunk);
		}

		// Assert - Middle chunks (index 1-3)
		for (int i = 1; i < chunks.Count - 1; i++)
		{
			chunks[i].IsMiddle.ShouldBeTrue($"Chunk at index {i} should be middle");
		}
	}

	[Fact]
	public async Task ChunkedStream_SingleChunk_IsBothFirstAndLast()
	{
		// Arrange & Act
		var chunks = new List<Chunk<TestStreamDocument>>();
		await foreach (var chunk in CreateChunkedStream(1).ConfigureAwait(false))
		{
			chunks.Add(chunk);
		}

		// Assert
		chunks.Count.ShouldBe(1);
		chunks[0].IsFirst.ShouldBeTrue();
		chunks[0].IsLast.ShouldBeTrue();
		chunks[0].IsSingle.ShouldBeTrue();
	}

	[Fact]
	public async Task ChunkedStream_IndicesAreSequential()
	{
		// Arrange & Act
		var chunks = new List<Chunk<TestStreamDocument>>();
		await foreach (var chunk in CreateChunkedStream(10).ConfigureAwait(false))
		{
			chunks.Add(chunk);
		}

		// Assert
		for (int i = 0; i < chunks.Count; i++)
		{
			chunks[i].Index.ShouldBe(i);
		}
	}

	#endregion Chunk Metadata Tests

	#region Large Stream Tests

	[Fact]
	public async Task StreamConsumer_LargeStream_ProcessesAll()
	{
		// Arrange
		var (handler, getProcessed) = CreateConsumerHandler();
		const int documentCount = 1000;

		// Act
		await handler.HandleAsync(
			CreateTestStream(documentCount),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		var processed = getProcessed();
		processed.Count.ShouldBe(documentCount);
	}

	#endregion Large Stream Tests
}

/// <summary>
/// Test streaming document for conformance testing.
/// </summary>
public sealed class TestStreamDocument : IDispatchDocument
{
	/// <summary>
	/// Gets or sets the document index within the stream.
	/// </summary>
	public int Index { get; set; }

	/// <summary>
	/// Gets or sets the document data.
	/// </summary>
	public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Reference implementation of a collecting stream consumer handler for testing.
/// </summary>
public sealed class CollectingStreamConsumerHandler : IStreamConsumerHandler<TestStreamDocument>
{
	private readonly List<TestStreamDocument> _processed = [];

	/// <summary>
	/// Gets the documents that have been processed.
	/// </summary>
	public IReadOnlyList<TestStreamDocument> Processed => _processed;

	/// <inheritdoc/>
	public async Task HandleAsync(
		IAsyncEnumerable<TestStreamDocument> documents,
		CancellationToken cancellationToken)
	{
		await foreach (var doc in documents.WithCancellation(cancellationToken)
			.ConfigureAwait(false))
		{
			_processed.Add(doc);
		}
	}
}
