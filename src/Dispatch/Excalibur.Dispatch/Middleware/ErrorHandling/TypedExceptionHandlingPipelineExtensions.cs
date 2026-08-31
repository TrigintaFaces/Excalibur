// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Middleware.ErrorHandling;

/// <summary>
/// Extension methods for adding typed exception handling to the dispatch pipeline.
/// </summary>
public static class TypedExceptionHandlingPipelineExtensions
{
	/// <summary>
	/// Adds <see cref="TypedExceptionHandlerMiddleware"/> to the dispatch pipeline so that
	/// <see cref="ITypedExceptionHandler{TException}"/> implementations registered in the container
	/// are resolved and invoked when a matching exception escapes a pipeline component below it.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// Registering a handler in the container is not on its own sufficient: this method is what places
	/// the middleware in the pipeline. Without it, no handler is ever consulted and every exception
	/// propagates.
	/// </para>
	/// <para>
	/// The middleware resolves the handler for the exception's exact type first, then walks the
	/// exception type hierarchy upward. <see cref="OperationCanceledException"/> is never routed to a
	/// handler. If no handler reports the exception as handled, it is re-thrown unchanged.
	/// </para>
	/// <para>
	/// An exception thrown by the message handler itself is not routed here: the terminal dispatch
	/// stage converts it into a failed result before the pipeline unwinds. Inspect the returned
	/// result for that case.
	/// </para>
	/// <example>
	/// <code>
	/// services.AddSingleton&lt;ITypedExceptionHandler&lt;NotFoundException&gt;, NotFoundExceptionHandler&gt;();
	/// services.AddDispatch(dispatch => dispatch.UseTypedExceptionHandling());
	/// </code>
	/// </example>
	/// </remarks>
	public static IDispatchBuilder UseTypedExceptionHandling(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<TypedExceptionHandlerMiddleware>();
	}
}
