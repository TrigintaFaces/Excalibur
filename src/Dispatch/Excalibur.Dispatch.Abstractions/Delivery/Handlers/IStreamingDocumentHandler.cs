// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines a handler that produces a stream of outputs from a document.
/// </summary>
/// <typeparam name="TDocument">The type of document to process.</typeparam>
/// <typeparam name="TOutput">The type of output items produced by the handler.</typeparam>
/// <remarks>
/// Implement this interface for document-to-stream transformations where a single document
/// produces multiple output items. This enables memory-efficient processing of large documents
/// without loading entire result sets into memory. Common use cases include:
/// <list type="bullet">
/// <item>CSV/JSON parsing: splitting files into individual records</item>
/// <item>Document splitting: breaking large documents into pages or sections</item>
/// <item>Entity extraction: extracting multiple entities from unstructured text</item>
/// <item>Report generation: streaming report rows to output</item>
/// <item>PDF processing: extracting pages or images from documents</item>
/// </list>
/// <para>
/// The covariant <c>out</c> modifier on <typeparamref name="TOutput"/> enables returning
/// streams of derived types where base types are expected. The contravariant <c>in</c> modifier
/// on <typeparamref name="TDocument"/> allows handlers to accept derived document types.
/// </para>
/// <para>
/// Handlers are resolved from dependency injection and should be registered with scoped lifetime
/// to maintain state within a single stream processing operation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class CsvRowHandler : IStreamingDocumentHandler&lt;CsvDocument, DataRow&gt;
/// {
///     public async IAsyncEnumerable&lt;DataRow&gt; HandleAsync(
///         CsvDocument document,
///         [EnumeratorCancellation] CancellationToken cancellationToken)
///     {
///         await foreach (var line in document.ReadLinesAsync(cancellationToken))
///         {
///             cancellationToken.ThrowIfCancellationRequested();
///             yield return ParseRow(line);
///         }
///     }
/// }
/// </code>
/// </example>
public interface IStreamingDocumentHandler<in TDocument, out TOutput>
	where TDocument : IDispatchDocument
{
	/// <summary>
	/// Handles the specified document and produces a stream of outputs asynchronously.
	/// </summary>
	/// <param name="document">The document to process.</param>
	/// <param name="cancellationToken">The cancellation token to observe throughout streaming.</param>
	/// <returns>An asynchronous stream of output items.</returns>
	/// <remarks>
	/// <para>
	/// Implementations should use <c>yield return</c> to produce items incrementally rather than
	/// building collections in memory. Apply the <c>[EnumeratorCancellation]</c> attribute to
	/// the cancellation token parameter when implementing this method.
	/// </para>
	/// <para>
	/// Check <paramref name="cancellationToken"/> periodically and between yield operations
	/// to ensure responsive cancellation. Use <c>ConfigureAwait(false)</c> on all await
	/// expressions when implementing in library code.
	/// </para>
	/// </remarks>
	IAsyncEnumerable<TOutput> HandleAsync(TDocument document, CancellationToken cancellationToken);
}
