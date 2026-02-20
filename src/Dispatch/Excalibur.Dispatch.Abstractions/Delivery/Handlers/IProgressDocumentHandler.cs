// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines a handler that processes a document with progress reporting.
/// </summary>
/// <typeparam name="TDocument">The type of document to process.</typeparam>
/// <remarks>
/// Implement this interface for long-running document operations where callers need
/// visibility into processing progress. This is useful for operations that may take
/// significant time and where users or monitoring systems need status updates.
/// Common use cases include:
/// <list type="bullet">
/// <item>Large file processing: importing, exporting, or transforming large files</item>
/// <item>Multi-step transformations: operations with distinct processing phases</item>
/// <item>Batch operations: processing many items where progress can be tracked</item>
/// <item>Report generation: creating complex reports with multiple sections</item>
/// <item>Data migrations: moving or transforming data between systems</item>
/// </list>
/// <para>
/// The contravariant <c>in</c> modifier on <typeparamref name="TDocument"/> allows handlers
/// to accept derived document types.
/// </para>
/// <para>
/// Progress is reported via the standard <see cref="IProgress{T}"/> pattern, allowing
/// callers to choose how to handle progress updates (UI updates, logging, metrics, etc.).
/// The <see cref="DocumentProgress"/> struct provides rich progress information including
/// percentage, item counts, and phase descriptions.
/// </para>
/// <para>
/// Handlers are resolved from dependency injection and should be registered with scoped lifetime
/// to maintain state within a single processing operation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class PdfExportHandler : IProgressDocumentHandler&lt;ExportDocument&gt;
/// {
///     public async Task HandleAsync(
///         ExportDocument document,
///         IProgress&lt;DocumentProgress&gt; progress,
///         CancellationToken cancellationToken)
///     {
///         var pages = document.GetPages();
///         var total = pages.Count;
///
///         for (int i = 0; i &lt; total; i++)
///         {
///             cancellationToken.ThrowIfCancellationRequested();
///
///             await ProcessPageAsync(pages[i], cancellationToken)
///                 .ConfigureAwait(false);
///
///             progress.Report(DocumentProgress.FromItems(
///                 itemsProcessed: i + 1,
///                 totalItems: total,
///                 currentPhase: $"Processing page {i + 1} of {total}"));
///         }
///
///         progress.Report(DocumentProgress.Completed(total, "Export complete"));
///     }
/// }
/// </code>
/// </example>
public interface IProgressDocumentHandler<in TDocument>
	where TDocument : IDispatchDocument
{
	/// <summary>
	/// Handles the specified document with progress reporting.
	/// </summary>
	/// <param name="document">The document to process.</param>
	/// <param name="progress">The progress reporter for status updates.</param>
	/// <param name="cancellationToken">The cancellation token to observe throughout processing.</param>
	/// <returns>A task that completes when the document has been fully processed.</returns>
	/// <remarks>
	/// <para>
	/// Implementations should report progress at regular intervals to provide meaningful
	/// feedback. Use <see cref="DocumentProgress.FromItems"/> for operations with known
	/// totals, or <see cref="DocumentProgress.Indeterminate"/> when the total is unknown.
	/// </para>
	/// <para>
	/// Check <paramref name="cancellationToken"/> periodically, especially before expensive
	/// operations, to ensure responsive cancellation. Use <c>ConfigureAwait(false)</c> on
	/// all await expressions when implementing in library code.
	/// </para>
	/// <para>
	/// The <paramref name="progress"/> parameter will never be null; callers that don't
	/// need progress can pass a no-op implementation.
	/// </para>
	/// </remarks>
	Task HandleAsync(
		TDocument document,
		IProgress<DocumentProgress> progress,
		CancellationToken cancellationToken);
}
