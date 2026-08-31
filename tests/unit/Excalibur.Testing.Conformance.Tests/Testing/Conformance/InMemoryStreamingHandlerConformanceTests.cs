// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Self-test proving <see cref="StreamingHandlerConformanceTestKit"/> runs end-to-end against a sample
/// <see cref="IStreamConsumerHandler{TestStreamDocument}"/> implementation and reports pass/fail
/// (wired-and-tested).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "PROVIDER")]
public sealed class InMemoryStreamingHandlerConformanceTests : StreamingHandlerConformanceTestKit
{
	/// <inheritdoc />
	protected override (IStreamConsumerHandler<TestStreamDocument> Handler, Func<IReadOnlyList<TestStreamDocument>> GetProcessed) CreateConsumerHandler()
	{
		var handler = new CollectingStreamConsumerHandler();
		return (handler, () => handler.Processed);
	}

	[Fact] public Task StreamConsumer_ProcessesAllDocuments_Test() => StreamConsumer_ProcessesAllDocuments();
	[Fact] public Task StreamConsumer_ReceivesDocumentsInOrder_Test() => StreamConsumer_ReceivesDocumentsInOrder();
	[Fact] public Task StreamConsumer_EmptyStream_CompletesSuccessfully_Test() => StreamConsumer_EmptyStream_CompletesSuccessfully();
	[Fact] public Task StreamConsumer_SingleDocument_ProcessedCorrectly_Test() => StreamConsumer_SingleDocument_ProcessedCorrectly();
	[Fact] public Task StreamConsumer_RespectsCancellation_Test() => StreamConsumer_RespectsCancellation();
	[Fact] public Task ChunkedStream_FirstChunkIsMarkedFirst_Test() => ChunkedStream_FirstChunkIsMarkedFirst();
	[Fact] public Task ChunkedStream_LastChunkIsMarkedLast_Test() => ChunkedStream_LastChunkIsMarkedLast();
	[Fact] public Task ChunkedStream_MiddleChunksAreMiddle_Test() => ChunkedStream_MiddleChunksAreMiddle();
	[Fact] public Task ChunkedStream_SingleChunk_IsBothFirstAndLast_Test() => ChunkedStream_SingleChunk_IsBothFirstAndLast();
	[Fact] public Task ChunkedStream_IndicesAreSequential_Test() => ChunkedStream_IndicesAreSequential();
	[Fact] public Task StreamConsumer_LargeStream_ProcessesAll_Test() => StreamConsumer_LargeStream_ProcessesAll();

	/// <summary>
	/// Reference collecting stream consumer handler used only to exercise the conformance kit.
	/// </summary>
	private sealed class CollectingStreamConsumerHandler : IStreamConsumerHandler<TestStreamDocument>
	{
		private readonly List<TestStreamDocument> _processed = [];

		public IReadOnlyList<TestStreamDocument> Processed => _processed;

		public async Task HandleAsync(
			IAsyncEnumerable<TestStreamDocument> documents,
			CancellationToken cancellationToken)
		{
			await foreach (var doc in documents.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				_processed.Add(doc);
			}
		}
	}

	#region Suite Wiring

	/// <summary>
	/// Fails if this suite stops exposing any arm the kit declares.
	/// </summary>
	/// <remarks>
	/// An arm nobody wires never executes, and an arm that never executes cannot fail - in the results it
	/// is indistinguishable from one that passed. That is why the wiring is checked rather than trusted to
	/// survive an edit: a new arm added to the shipped kit turns this red here instead of going silently
	/// unrun.
	/// </remarks>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	#endregion
}
