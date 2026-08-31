// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Middleware.ErrorHandling;

/// <summary>
/// Defines a typed exception handler that processes exceptions of a specific type
/// within the dispatch pipeline.
/// </summary>
/// <typeparam name="TException">The type of exception this handler processes.</typeparam>
/// <remarks>
/// <para>
/// Typed exception handlers enable fine-grained exception handling by routing specific
/// exception types to dedicated handlers, in the shape of ASP.NET Core's
/// <c>IExceptionHandler</c>. The scope differs from that of ASP.NET Core -- see below.
/// </para>
/// <para>
/// Register handlers in the container and add the middleware to the pipeline with
/// <see cref="TypedExceptionHandlingPipelineExtensions.UseTypedExceptionHandling"/>.
/// <see cref="TypedExceptionHandlerMiddleware"/> then resolves the matching handler when an
/// exception escapes a pipeline component below it. Registration alone does not place the
/// middleware in the pipeline, and without it no handler is consulted.
/// </para>
/// <para>
/// <b>Scope.</b> Handlers see faults raised by pipeline components. They do not see an exception
/// thrown by the message handler itself: the terminal dispatch stage converts that into a failed
/// <see cref="IMessageResult"/> before the pipeline unwinds, so no middleware observes it. Inspect
/// the returned result for that case.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class TenantResolutionFailureHandler : ITypedExceptionHandler&lt;TenantResolutionException&gt;
/// {
///     public ValueTask&lt;ExceptionHandlerResult&gt; HandleAsync(
///         TenantResolutionException exception,
///         IDispatchMessage message,
///         IMessageContext context,
///         CancellationToken cancellationToken)
///     {
///         return ValueTask.FromResult(ExceptionHandlerResult.Handled(
///             MessageResult.Failed("Tenant could not be resolved")));
///     }
/// }
/// </code>
/// </example>
public interface ITypedExceptionHandler<in TException>
	where TException : Exception
{
	/// <summary>
	/// Handles the specified exception that occurred during message processing.
	/// </summary>
	/// <param name="exception">The exception to handle.</param>
	/// <param name="message">The message being processed when the exception occurred.</param>
	/// <param name="context">The message context at the time of the exception.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>
	/// A <see cref="ValueTask{TResult}"/> containing an <see cref="ExceptionHandlerResult"/>
	/// that indicates whether the exception was handled and the result to return.
	/// </returns>
	ValueTask<ExceptionHandlerResult> HandleAsync(
		TException exception,
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken);
}
