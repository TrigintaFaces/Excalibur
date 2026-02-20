// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines a handler that consumes a stream of documents.
/// </summary>
/// <typeparam name="TDocument">The type of documents in the stream.</typeparam>
/// <remarks>
/// Implement this interface to process an incoming stream of documents. This enables
/// memory-efficient batch processing where documents arrive incrementally. The handler
/// controls consumption rate, providing natural backpressure. Common use cases include:
/// <list type="bullet">
/// <item>Batch imports: processing CSV/JSON rows as they are parsed</item>
/// <item>ETL sinks: writing transformed records to storage</item>
/// <item>Message queue consumers: processing message streams</item>
/// <item>Real-time data ingestion: handling continuous data feeds</item>
/// <item>Aggregation pipelines: computing statistics over streams</item>
/// </list>
/// <para>
/// The contravariant <c>in</c> modifier on <typeparamref name="TDocument"/> allows handlers
/// to accept streams of derived document types.
/// </para>
/// <para>
/// Handlers are resolved from dependency injection and should be registered with scoped lifetime
/// to maintain state within a single stream processing operation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class BatchImportHandler : IStreamConsumerHandler&lt;DataRow&gt;
/// {
///     private readonly IDatabase _database;
///
///     public BatchImportHandler(IDatabase database) =&gt; _database = database;
///
///     public async Task HandleAsync(
///         IAsyncEnumerable&lt;DataRow&gt; documents,
///         CancellationToken cancellationToken)
///     {
///         var batch = new List&lt;DataRow&gt;();
///         await foreach (var row in documents.WithCancellation(cancellationToken)
///             .ConfigureAwait(false))
///         {
///             batch.Add(row);
///             if (batch.Count &gt;= 1000)
///             {
///                 await _database.BulkInsertAsync(batch, cancellationToken)
///                     .ConfigureAwait(false);
///                 batch.Clear();
///             }
///         }
///         if (batch.Count &gt; 0)
///         {
///             await _database.BulkInsertAsync(batch, cancellationToken)
///                 .ConfigureAwait(false);
///         }
///     }
/// }
/// </code>
/// </example>
public interface IStreamConsumerHandler<in TDocument>
	where TDocument : IDispatchDocument
{
	/// <summary>
	/// Handles the incoming stream of documents asynchronously.
	/// </summary>
	/// <param name="documents">The stream of documents to process.</param>
	/// <param name="cancellationToken">The cancellation token to observe throughout processing.</param>
	/// <returns>A task that completes when the stream has been fully processed.</returns>
	/// <remarks>
	/// <para>
	/// The handler controls consumption rate by the speed at which it iterates through
	/// <paramref name="documents"/>. This provides natural backpressure to upstream producers.
	/// </para>
	/// <para>
	/// Use <c>WithCancellation(cancellationToken)</c> when iterating to ensure proper
	/// cancellation propagation. Use <c>ConfigureAwait(false)</c> on all await expressions
	/// when implementing in library code.
	/// </para>
	/// <para>
	/// The task completes when all documents have been processed. If cancellation is requested,
	/// the task should throw <see cref="OperationCanceledException"/>.
	/// </para>
	/// </remarks>
	Task HandleAsync(IAsyncEnumerable<TDocument> documents, CancellationToken cancellationToken);
}
