// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0007 // Use implicit type (var)

using System.Runtime.CompilerServices;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Streaming;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="IStreamConsumerHandler{TDocument}"/> implementations and
/// streaming pipeline integration.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this kit and implement <see cref="CreateConsumerHandler"/> to verify that your streaming
/// consumer handler receives all documents in order, respects cancellation, and handles empty, single, and
/// large streams. The kit uses <see cref="TestStreamDocument"/> as its document type.
/// </para>
/// <para>
/// The kit exposes plain <c>public virtual</c> methods with no test-framework attributes; add the
/// attributes your test framework requires (for example <c>[Fact]</c>) on thin overrides in your derived
/// class.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class StreamingHandlerConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a stream consumer handler for testing together with an accessor that returns the documents
	/// it has processed.
	/// </summary>
	/// <returns>A stream consumer handler and an accessor to retrieve processed items.</returns>
	protected abstract (IStreamConsumerHandler<TestStreamDocument> Handler, Func<IReadOnlyList<TestStreamDocument>> GetProcessed) CreateConsumerHandler();

	/// <summary>
	/// Creates an async enumerable of test documents.
	/// </summary>
	/// <param name="count">The number of documents to yield.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>An asynchronous sequence of <see cref="TestStreamDocument"/>.</returns>
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
	/// <param name="count">The number of chunks to yield.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>An asynchronous sequence of <see cref="Chunk{TestStreamDocument}"/>.</returns>
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

	/// <summary>Verifies the consumer processes every document in the stream.</summary>
	public virtual async Task StreamConsumer_ProcessesAllDocuments()
	{
		var (handler, getProcessed) = CreateConsumerHandler();
		const int documentCount = 10;

		await handler.HandleAsync(CreateTestStream(documentCount), CancellationToken.None).ConfigureAwait(false);

		var processed = getProcessed();
		if (processed.Count != documentCount)
		{
			throw new TestFixtureAssertionException(
				$"Expected {documentCount} documents processed but was {processed.Count}.");
		}
	}

	/// <summary>Verifies the consumer receives documents in stream order.</summary>
	public virtual async Task StreamConsumer_ReceivesDocumentsInOrder()
	{
		var (handler, getProcessed) = CreateConsumerHandler();
		const int documentCount = 20;

		await handler.HandleAsync(CreateTestStream(documentCount), CancellationToken.None).ConfigureAwait(false);

		var processed = getProcessed();
		for (int i = 0; i < processed.Count; i++)
		{
			if (processed[i].Index != i)
			{
				throw new TestFixtureAssertionException(
					$"Document at position {i} should have index {i} but was {processed[i].Index}.");
			}
		}
	}

	/// <summary>Verifies an empty stream completes successfully with no processed documents.</summary>
	public virtual async Task StreamConsumer_EmptyStream_CompletesSuccessfully()
	{
		var (handler, getProcessed) = CreateConsumerHandler();

		await handler.HandleAsync(CreateTestStream(0), CancellationToken.None).ConfigureAwait(false);

		var processed = getProcessed();
		if (processed.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected an empty stream to process 0 documents but was {processed.Count}.");
		}
	}

	/// <summary>Verifies a single-document stream is processed correctly.</summary>
	public virtual async Task StreamConsumer_SingleDocument_ProcessedCorrectly()
	{
		var (handler, getProcessed) = CreateConsumerHandler();

		await handler.HandleAsync(CreateTestStream(1), CancellationToken.None).ConfigureAwait(false);

		var processed = getProcessed();
		if (processed.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected 1 document processed but was {processed.Count}.");
		}

		if (processed[0].Data != "Document-0")
		{
			throw new TestFixtureAssertionException(
				$"Expected the single document data to be 'Document-0' but was '{processed[0].Data}'.");
		}
	}

	/// <summary>Verifies the consumer honors cancellation by throwing <see cref="OperationCanceledException"/>.</summary>
	public virtual async Task StreamConsumer_RespectsCancellation()
	{
		var (handler, _) = CreateConsumerHandler();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		try
		{
			await handler.HandleAsync(CreateTestStream(100, cts.Token), cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		throw new TestFixtureAssertionException(
			"Expected HandleAsync to throw OperationCanceledException when cancelled.");
	}

	/// <summary>Verifies the first chunk is marked first (and not last) with index 0.</summary>
	public virtual async Task ChunkedStream_FirstChunkIsMarkedFirst()
	{
		var chunks = await CollectChunksAsync(5).ConfigureAwait(false);

		if (!chunks[0].IsFirst || chunks[0].IsLast || chunks[0].Index != 0)
		{
			throw new TestFixtureAssertionException(
				"Expected the first chunk to be IsFirst=true, IsLast=false, Index=0.");
		}
	}

	/// <summary>Verifies the last chunk is marked last (and not first) with the final index.</summary>
	public virtual async Task ChunkedStream_LastChunkIsMarkedLast()
	{
		var chunks = await CollectChunksAsync(5).ConfigureAwait(false);

		if (!chunks[^1].IsLast || chunks[^1].IsFirst || chunks[^1].Index != 4)
		{
			throw new TestFixtureAssertionException(
				"Expected the last chunk to be IsLast=true, IsFirst=false, Index=4.");
		}
	}

	/// <summary>Verifies middle chunks report <c>IsMiddle</c>.</summary>
	public virtual async Task ChunkedStream_MiddleChunksAreMiddle()
	{
		var chunks = await CollectChunksAsync(5).ConfigureAwait(false);

		for (int i = 1; i < chunks.Count - 1; i++)
		{
			if (!chunks[i].IsMiddle)
			{
				throw new TestFixtureAssertionException($"Chunk at index {i} should be middle.");
			}
		}
	}

	/// <summary>Verifies a single chunk is both first and last (and single).</summary>
	public virtual async Task ChunkedStream_SingleChunk_IsBothFirstAndLast()
	{
		var chunks = await CollectChunksAsync(1).ConfigureAwait(false);

		if (chunks.Count != 1 || !chunks[0].IsFirst || !chunks[0].IsLast || !chunks[0].IsSingle)
		{
			throw new TestFixtureAssertionException(
				"Expected a single chunk to be IsFirst=true, IsLast=true, IsSingle=true.");
		}
	}

	/// <summary>Verifies chunk indices are sequential.</summary>
	public virtual async Task ChunkedStream_IndicesAreSequential()
	{
		var chunks = await CollectChunksAsync(10).ConfigureAwait(false);

		for (int i = 0; i < chunks.Count; i++)
		{
			if (chunks[i].Index != i)
			{
				throw new TestFixtureAssertionException(
					$"Expected chunk at position {i} to have index {i} but was {chunks[i].Index}.");
			}
		}
	}

	/// <summary>Verifies a large stream is processed in full without loss.</summary>
	public virtual async Task StreamConsumer_LargeStream_ProcessesAll()
	{
		var (handler, getProcessed) = CreateConsumerHandler();
		const int documentCount = 1000;

		await handler.HandleAsync(CreateTestStream(documentCount), CancellationToken.None).ConfigureAwait(false);

		var processed = getProcessed();
		if (processed.Count != documentCount)
		{
			throw new TestFixtureAssertionException(
				$"Expected {documentCount} documents processed but was {processed.Count}.");
		}
	}

	private static async Task<List<Chunk<TestStreamDocument>>> CollectChunksAsync(int count)
	{
		var chunks = new List<Chunk<TestStreamDocument>>();
		await foreach (var chunk in CreateChunkedStream(count).ConfigureAwait(false))
		{
			chunks.Add(chunk);
		}

		return chunks;
	}

}

/// <summary>
/// A test streaming document used by <see cref="StreamingHandlerConformanceTestKit"/>.
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
