// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a middleware component that can intercept and process messages in the dispatch pipeline.
/// </summary>
/// <remarks>
/// Middleware components form a pipeline where each component can:
/// <list type="bullet">
/// <item> Inspect and modify the message or context before processing </item>
/// <item> Execute logic before and after the next middleware component </item>
/// <item> Short-circuit the pipeline by not calling the next delegate </item>
/// <item> Handle cross-cutting concerns like validation, authorization, caching, and logging </item>
/// </list>
/// Middleware executes in a specific order determined by the Stage property. Common middleware include: validation, authorization, routing,
/// versioning, caching, metrics, and error handling. Implementations should be thread-safe and avoid storing state between invocations.
/// </remarks>
public interface IDispatchMiddleware
{
	/// <summary>
	/// Gets the pipeline stage where this middleware should execute.
	/// </summary>
	/// <remarks>
	/// The stage determines the execution order of middleware in the pipeline. If null, the middleware will be registered at the end of the
	/// pipeline. Stages are processed in numeric order, allowing fine-grained control over middleware execution sequence.
	/// </remarks>
	/// <value> The pipeline stage for this middleware. </value>
	DispatchMiddlewareStage? Stage { get; }

	/// <summary>
	/// Gets the message kinds that this middleware applies.
	/// </summary>
	/// <remarks>
	/// Specifies which types of messages this middleware should process. The pipeline filters middleware based on these flags, ensuring
	/// only relevant middleware processes each message. This improves performance and allows middleware to specialize in handling specific
	/// message types. If not implemented, defaults to <see cref="MessageKinds.All" /> for backward compatibility.
	/// </remarks>
	/// <value> The set of message kinds this middleware applies to. </value>
	MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <summary>
	/// Processes the message within the middleware pipeline.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context containing metadata and processing state. </param>
	/// <param name="nextDelegate"> The delegate to invoke the next middleware in the pipeline. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns>
	/// A <see cref="ValueTask{TResult}"/> representing the result of message processing, potentially modified by this middleware.
	/// Using ValueTask enables zero-allocation dispatch on synchronous completion paths.
	/// </returns>
	/// <remarks>
	/// Implementations can execute logic before calling nextDelegate, after calling it, or both. To short-circuit the pipeline, don't call
	/// nextDelegate and return an appropriate result. The context can be used to share data between middleware components. Respect the
	/// cancellation token to support timeouts and graceful shutdown.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when message, context, or nextDelegate is null. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
	DispatchRequestDelegate nextDelegate,
	CancellationToken cancellationToken);
}
