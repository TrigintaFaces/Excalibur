// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware.ErrorHandling;

/// <summary>
/// Defines a typed exception handler that processes exceptions of a specific type
/// within the dispatch pipeline.
/// </summary>
/// <typeparam name="TException">The type of exception this handler processes.</typeparam>
/// <remarks>
/// <para>
/// Typed exception handlers enable fine-grained exception handling by routing specific
/// exception types to dedicated handlers. This follows the Microsoft pattern of type-based
/// dispatch (similar to <c>IExceptionHandler</c> in ASP.NET Core).
/// </para>
/// <para>
/// Register handlers via DI and they will be automatically resolved by
/// <see cref="TypedExceptionHandlerMiddleware"/> when a matching exception occurs.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class NotFoundExceptionHandler : ITypedExceptionHandler&lt;NotFoundException&gt;
/// {
///     public ValueTask&lt;ExceptionHandlerResult&gt; HandleAsync(
///         NotFoundException exception,
///         IDispatchMessage message,
///         IMessageContext context,
///         CancellationToken cancellationToken)
///     {
///         return ValueTask.FromResult(ExceptionHandlerResult.Handled(
///             MessageResult.Failed("Resource not found")));
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
