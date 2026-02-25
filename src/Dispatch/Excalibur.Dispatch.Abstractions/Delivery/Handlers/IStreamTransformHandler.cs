// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines a handler that transforms an input stream into an output stream.
/// </summary>
/// <typeparam name="TInput">The type of input documents in the stream.</typeparam>
/// <typeparam name="TOutput">The type of output items produced by the transformation.</typeparam>
/// <remarks>
/// Implement this interface for stream-to-stream transformations where an input stream
/// is transformed into an output stream. This enables composable streaming pipelines
/// with memory-efficient processing. Common use cases include:
/// <list type="bullet">
/// <item>Data enrichment: augmenting records with additional data from external sources</item>
/// <item>Format conversion: transforming records from one format to another</item>
/// <item>Filtering: selecting records that match specific criteria</item>
/// <item>Aggregation: grouping or summarizing input records into output summaries</item>
/// <item>Batching: combining multiple input items into batch outputs</item>
/// <item>Flattening: expanding single inputs into multiple outputs</item>
/// </list>
/// <para>
/// The contravariant <c>in</c> modifier on <typeparamref name="TInput"/> allows handlers
/// to accept streams of derived document types. The covariant <c>out</c> modifier on
/// <typeparamref name="TOutput"/> enables returning streams of derived types where base
/// types are expected.
/// </para>
/// <para>
/// Transform handlers can be chained together to form streaming pipelines where the output
/// of one handler becomes the input to the next, without materializing intermediate results.
/// </para>
/// <para>
/// Handlers are resolved from dependency injection and should be registered with scoped lifetime
/// to maintain state within a single stream processing operation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class EnrichmentHandler : IStreamTransformHandler&lt;CustomerRecord, EnrichedCustomer&gt;
/// {
///     private readonly IExternalService _service;
///
///     public EnrichmentHandler(IExternalService service) =&gt; _service = service;
///
///     public async IAsyncEnumerable&lt;EnrichedCustomer&gt; HandleAsync(
///         IAsyncEnumerable&lt;CustomerRecord&gt; input,
///         [EnumeratorCancellation] CancellationToken cancellationToken)
///     {
///         await foreach (var record in input.WithCancellation(cancellationToken)
///             .ConfigureAwait(false))
///         {
///             var enriched = await _service.EnrichAsync(record, cancellationToken)
///                 .ConfigureAwait(false);
///             yield return enriched;
///         }
///     }
/// }
/// </code>
/// </example>
public interface IStreamTransformHandler<in TInput, out TOutput>
	where TInput : IDispatchDocument
{
	/// <summary>
	/// Transforms the input stream into an output stream asynchronously.
	/// </summary>
	/// <param name="input">The input stream of documents to transform.</param>
	/// <param name="cancellationToken">The cancellation token to observe throughout the transformation.</param>
	/// <returns>An asynchronous stream of transformed output items.</returns>
	/// <remarks>
	/// <para>
	/// Implementations should consume the input stream incrementally and yield outputs
	/// as they become available. This enables efficient pipeline processing without
	/// materializing intermediate collections.
	/// </para>
	/// <para>
	/// Apply the <c>[EnumeratorCancellation]</c> attribute to the cancellation token parameter
	/// when implementing this method. Use <c>WithCancellation(cancellationToken)</c> when
	/// iterating the input stream to ensure proper cancellation propagation.
	/// </para>
	/// <para>
	/// Transformations may produce zero, one, or multiple outputs per input item depending
	/// on the transformation logic (filtering, one-to-one, or one-to-many mapping).
	/// </para>
	/// </remarks>
	IAsyncEnumerable<TOutput> HandleAsync(IAsyncEnumerable<TInput> input, CancellationToken cancellationToken);
}
