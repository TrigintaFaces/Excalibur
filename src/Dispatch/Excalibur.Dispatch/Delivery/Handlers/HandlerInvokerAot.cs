// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



#if USE_SOURCE_GENERATION || PUBLISH_AOT
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// AOT-compatible handler invoker that uses source-generated code instead of expression compilation.
/// </summary>
/// <remarks>
/// This implementation avoids reflection and expression compilation, making it suitable for Native AOT scenarios. It delegates to the
/// source-generated <see cref="HandlerInvokerRegistry"/> which contains compile-time generated methods for known handler and message type combinations.
/// For non-AOT scenarios, use <see cref="HandlerInvoker"/> instead.
/// </remarks>
public sealed class HandlerInvokerAot : IHandlerInvoker, IValueTaskHandlerInvoker
{
/// <summary>
/// Invokes a handler's HandleAsync method using AOT-compatible source-generated code.
/// </summary>
/// <param name="handler"> The handler instance to invoke. </param>
/// <param name="message"> The message to handle. </param>
/// <param name="cancellationToken"> Cancellation token for the operation. </param>
/// <returns> The result of handler execution, or null for void handlers. </returns>
/// <remarks>
/// This method uses source-generated code to avoid reflection and expression compilation. The <see cref="HandlerInvokerRegistry"/>
/// is populated at compile time by scanning all handler types and generating optimized invocation code for each handler and message type combination.
/// </remarks>
	[RequiresUnreferencedCode("Handler invocation may require reflection to call handler methods")]
	public Task<object?> InvokeAsync(object handler, IDispatchMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);
		ArgumentNullException.ThrowIfNull(message);

		var invoker = HandlerInvokerRegistry.GetInvoker(handler.GetType())
			?? throw new InvalidOperationException($"No invoker registered for handler type {handler.GetType().FullName}");

		return invoker(handler, message, cancellationToken);
	}

	/// <summary>
	/// Invokes a handler using AOT-compatible source-generated code and returns a <see cref="ValueTask{TResult}"/>.
	/// </summary>
	public ValueTask<object?> InvokeValueTaskAsync(object handler, IDispatchMessage message, CancellationToken cancellationToken)
		=> new(InvokeAsync(handler, message, cancellationToken));
}

#endif
