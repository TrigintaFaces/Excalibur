// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines methods for dispatching messages through the Dispatch pipeline.
/// </summary>
public interface IDispatcher
{
	/// <summary>
	/// Gets the service provider used by this dispatcher for resolving dependencies.
	/// </summary>
	/// <value>
	/// The <see cref="IServiceProvider"/> instance, or <see langword="null"/> if not available.
	/// </value>
	/// <remarks>
	/// <para>
	/// This property enables convenience extension methods to access DI services like
	/// <see cref="Delivery.IMessageContextFactory"/> without requiring explicit parameters.
	/// </para>
	/// <para>
	/// The service provider may be <see langword="null"/> in unit testing scenarios or when
	/// the dispatcher is used outside of a DI container.
	/// </para>
	/// </remarks>
	IServiceProvider? ServiceProvider { get; }

	/// <summary>
	/// Dispatches a message without expecting a return value.
	/// </summary>
	/// <typeparam name="TMessage"> Type of message being dispatched. </typeparam>
	/// <param name="message"> The message instance. </param>
	/// <param name="context"> Context for the dispatch operation. </param>
	/// <param name="cancellationToken"> Token used to cancel the operation. </param>
	/// <returns> The result of dispatch execution. </returns>
	// R0.8: Do not add multiple overloads with optional parameters - Necessary for interface usability
#pragma warning disable RS0026

	Task<IMessageResult> DispatchAsync<TMessage>(
		TMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage;

#pragma warning restore RS0026

	/// <summary>
	/// Dispatches a message that expects a return value.
	/// </summary>
	/// <typeparam name="TMessage"> Type of message being dispatched. </typeparam>
	/// <typeparam name="TResponse"> Expected response type. </typeparam>
	/// <param name="message"> The message instance. </param>
	/// <param name="context"> Context for the dispatch operation. </param>
	/// <param name="cancellationToken"> Token used to cancel the operation. </param>
	/// <returns> The result including the response value. </returns>
	// R0.8: Do not add multiple overloads with optional parameters - Necessary for interface usability
#pragma warning disable RS0026

	Task<IMessageResult<TResponse>> DispatchAsync<TMessage, TResponse>(
		TMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
#pragma warning restore RS0026
		where TMessage : IDispatchAction<TResponse>;

	/// <summary>
	/// Dispatches a document and returns a stream of outputs from the streaming handler.
	/// </summary>
	/// <typeparam name="TDocument">Type of document being dispatched.</typeparam>
	/// <typeparam name="TOutput">Type of output items produced by the handler.</typeparam>
	/// <param name="document">The document to process.</param>
	/// <param name="context">Context for the dispatch operation.</param>
	/// <param name="cancellationToken">Token used to cancel the operation and streaming.</param>
	/// <returns>An asynchronous stream of output items produced by the handler.</returns>
	/// <remarks>
	/// <para>
	/// This method resolves an <see cref="IStreamingDocumentHandler{TDocument, TOutput}"/>
	/// from the service provider and invokes it with the document. The returned stream should be
	/// consumed using <c>await foreach</c> to enable memory-efficient processing.
	/// </para>
	/// <para>
	/// The cancellation token is propagated throughout the streaming operation. Consumers
	/// should use <c>WithCancellation()</c> when iterating to ensure responsive cancellation.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// await foreach (var row in dispatcher.DispatchStreamingAsync&lt;CsvDocument, DataRow&gt;(
	///     document, context, cancellationToken))
	/// {
	///     ProcessRow(row);
	/// }
	/// </code>
	/// </example>
	IAsyncEnumerable<TOutput> DispatchStreamingAsync<TDocument, TOutput>(
		TDocument document,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TDocument : IDispatchDocument;

	/// <summary>
	/// Dispatches a stream of documents to a consumer handler.
	/// </summary>
	/// <typeparam name="TDocument">Type of documents in the stream.</typeparam>
	/// <param name="documents">The stream of documents to process.</param>
	/// <param name="context">Context for the dispatch operation.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>A task that completes when the stream has been fully processed.</returns>
	/// <remarks>
	/// <para>
	/// This method resolves an <see cref="IStreamConsumerHandler{TDocument}"/>
	/// from the service provider and invokes it with the document stream. The handler
	/// controls consumption rate, providing natural backpressure to upstream producers.
	/// </para>
	/// <para>
	/// The task completes when the entire stream has been consumed. If cancellation is
	/// requested, an <see cref="OperationCanceledException"/> is thrown.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var rows = ParseCsvAsync(fileStream, cancellationToken);
	/// await dispatcher.DispatchStreamAsync&lt;DataRow&gt;(
	///     rows, context, cancellationToken);
	/// </code>
	/// </example>
	Task DispatchStreamAsync<TDocument>(
		IAsyncEnumerable<TDocument> documents,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TDocument : IDispatchDocument;

	/// <summary>
	/// Dispatches an input stream through a transform handler and returns the output stream.
	/// </summary>
	/// <typeparam name="TInput">Type of input documents in the stream.</typeparam>
	/// <typeparam name="TOutput">Type of output items produced by the transformation.</typeparam>
	/// <param name="input">The input stream of documents to transform.</param>
	/// <param name="context">Context for the dispatch operation.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>An asynchronous stream of transformed output items.</returns>
	/// <remarks>
	/// <para>
	/// This method resolves an <see cref="IStreamTransformHandler{TInput, TOutput}"/>
	/// from the service provider and invokes it with the input stream. Transform handlers
	/// enable composable streaming pipelines for data enrichment, filtering, and format conversion.
	/// </para>
	/// <para>
	/// The transformation is performed lazily as items are consumed from the output stream.
	/// This enables memory-efficient processing of large data sets without materializing
	/// intermediate collections.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var enriched = dispatcher.DispatchTransformStreamAsync&lt;CustomerRecord, EnrichedCustomer&gt;(
	///     records, context, cancellationToken);
	/// await foreach (var customer in enriched.WithCancellation(cancellationToken))
	/// {
	///     await SaveAsync(customer);
	/// }
	/// </code>
	/// </example>
	IAsyncEnumerable<TOutput> DispatchTransformStreamAsync<TInput, TOutput>(
		IAsyncEnumerable<TInput> input,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TInput : IDispatchDocument;

	/// <summary>
	/// Dispatches a document to a progress-reporting handler.
	/// </summary>
	/// <typeparam name="TDocument">Type of document being dispatched.</typeparam>
	/// <param name="document">The document to process.</param>
	/// <param name="context">Context for the dispatch operation.</param>
	/// <param name="progress">The progress reporter for status updates.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>A task that completes when the document has been fully processed.</returns>
	/// <remarks>
	/// <para>
	/// This method resolves an <see cref="IProgressDocumentHandler{TDocument}"/>
	/// from the service provider and invokes it with the document and progress reporter.
	/// Use this for long-running operations where progress visibility is needed.
	/// </para>
	/// <para>
	/// The <paramref name="progress"/> parameter receives <see cref="DocumentProgress"/>
	/// updates as the operation proceeds. Callers can use this for UI updates, logging,
	/// or metrics collection.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var progress = new Progress&lt;DocumentProgress&gt;(p =&gt;
	///     Console.WriteLine($"{p.PercentComplete:F1}% - {p.CurrentPhase}"));
	/// await dispatcher.DispatchWithProgressAsync(
	///     document, context, progress, cancellationToken);
	/// </code>
	/// </example>
	Task DispatchWithProgressAsync<TDocument>(
		TDocument document,
		IMessageContext context,
		IProgress<DocumentProgress> progress,
		CancellationToken cancellationToken)
		where TDocument : IDispatchDocument;
}
