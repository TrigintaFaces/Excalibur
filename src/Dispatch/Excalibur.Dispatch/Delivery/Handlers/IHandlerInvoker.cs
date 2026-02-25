// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Defines the contract for handler invocation services that execute handler methods with messages. The invoker is responsible for calling
/// the appropriate handler method with proper type safety and result handling, abstracting the invocation details from the message
/// processing pipeline.
/// </summary>
/// <remarks>
/// Handler invokers bridge the gap between generic message processing infrastructure and strongly-typed handler implementations. They
/// handle the complexity of method resolution, parameter binding, and result extraction while maintaining type safety and performance
/// characteristics appropriate for high-throughput message processing scenarios.
/// </remarks>
public interface IHandlerInvoker
{
	/// <summary>
	/// Asynchronously invokes the appropriate handler method with the specified message and returns any result produced by the handler execution.
	/// </summary>
	/// <param name="handler"> The handler instance to invoke, typically created by <see cref="IHandlerActivator" />. </param>
	/// <param name="message"> The message to process, which will be passed as a parameter to the handler method. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during handler execution. </param>
	/// <returns>
	/// A task that represents the asynchronous handler invocation, containing the result returned by the handler if applicable, or <c> null
	/// </c> for handlers that don't return results.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="handler" /> or <paramref name="message" /> is null. </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the handler doesn't have a compatible method for processing the message type.
	/// </exception>
	/// <remarks>
	/// The invoker must handle various handler patterns including:
	/// - Synchronous handlers that return results directly
	/// - Asynchronous handlers that return Task or Task&lt;T&gt;
	/// - Void handlers that don't return results
	/// - Generic handlers with strongly-typed message parameters
	///
	/// Implementations should provide efficient method resolution and invocation, potentially using compiled expressions, source
	/// generation, or other high-performance techniques to minimize reflection overhead during message processing.
	/// </remarks>
	[RequiresUnreferencedCode("Handler invocation may require reflection to call handler methods")]
	Task<object?> InvokeAsync(object handler, IDispatchMessage message, CancellationToken cancellationToken);
}
