// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines a handler for an action with no return value. <b>This is the recommended handler interface for commands.</b>
/// </summary>
/// <typeparam name="TAction"> The type of action to handle. </typeparam>
/// <remarks>
/// <para>
/// <b>Recommended for most use cases.</b> The framework automatically wraps the completion
/// in <see cref="IMessageResult"/>. Use exception throwing for failures; the exception mapping
/// middleware converts exceptions to proper failure results.
/// </para>
/// <para>
/// Common use cases include:
/// </para>
/// <list type="bullet">
/// <item><description>Commands that modify system state</description></item>
/// <item><description>Fire-and-forget operations</description></item>
/// <item><description>Asynchronous tasks that report completion through events</description></item>
/// </list>
/// <para>
/// Handlers are resolved from dependency injection and should be registered with appropriate
/// lifetime (typically scoped or transient).
/// </para>
/// <para>
/// For advanced scenarios requiring direct control over <see cref="IMessageResult"/>
/// (e.g., cache hits, custom result metadata), use <see cref="IDispatchHandler{TMessage}"/> instead.
/// </para>
/// </remarks>
public interface IActionHandler<in TAction>
	where TAction : IDispatchAction
{
	/// <summary>
	/// Handles the specified action asynchronously.
	/// </summary>
	/// <param name="action"> The action to handle. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <remarks>
	/// Implementations should respect the cancellation token and handle exceptions appropriately. The framework ensures this method is
	/// called within the message processing pipeline with proper context.
	/// </remarks>
	Task HandleAsync(TAction action, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a handler for an action that returns a result. <b>This is the recommended handler interface for queries.</b>
/// </summary>
/// <typeparam name="TAction"> The type of action to handle. </typeparam>
/// <typeparam name="TResult"> The type of result returned by the handler. </typeparam>
/// <remarks>
/// <para>
/// <b>Recommended for most use cases.</b> Return your business type directly; the framework
/// automatically wraps it in <see cref="IMessageResult{TResult}"/>. Use exception throwing for
/// failures; the exception mapping middleware converts exceptions to proper failure results.
/// </para>
/// <para>
/// Common use cases include:
/// </para>
/// <list type="bullet">
/// <item><description>Query handlers that fetch and return data</description></item>
/// <item><description>Commands that return created entity IDs</description></item>
/// <item><description>Validation operations that return detailed results</description></item>
/// <item><description>Calculation or transformation operations</description></item>
/// </list>
/// <para>
/// For advanced scenarios requiring direct control over <see cref="IMessageResult{TResult}"/>
/// (e.g., cache hits with <c>CacheHit = true</c>, custom result metadata), use
/// <see cref="IDispatchHandler{TMessage}"/> instead.
/// </para>
/// </remarks>
public interface IActionHandler<in TAction, TResult>
	where TAction : IDispatchAction<TResult>
{
	/// <summary>
	/// Handles the specified action and returns a result asynchronously.
	/// </summary>
	/// <param name="action"> The action to handle. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task containing the result of the action. </returns>
	/// <remarks>
	/// Implementations should respect the cancellation token and handle exceptions appropriately. The returned result will be wrapped in an
	/// IMessageResult by the framework for transport across boundaries.
	/// </remarks>
	Task<TResult> HandleAsync(TAction action, CancellationToken cancellationToken);
}
