// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Defines an ultra-local success path for command/query dispatch with minimal overhead.
/// </summary>
/// <remarks>
/// <para>
/// These methods are optimized for local in-process execution and avoid creating an
/// <see cref="IMessageContext"/> unless the dispatch path requires it.
/// </para>
/// <para>
/// This is a deliberate fast-path primitive. Its methods <b>bypass routing and the entire middleware
/// pipeline</b> — including validation, authorization, and telemetry. Use it only for message types that
/// have no such cross-cutting requirements. For any message that must be validated, authorized, or observed,
/// call <see cref="IDispatcher"/>.DispatchAsync instead, which runs
/// the full pipeline for the same type.
/// </para>
/// </remarks>
public interface IDirectLocalDispatcher
{

	/// <summary>
	/// Gets a value indicating whether dispatching <paramref name="messageType"/> may skip the middleware
	/// pipeline without changing observable behaviour.
	/// </summary>
	/// <param name="messageType">The message type about to be dispatched.</param>
	/// <returns>
	/// <see langword="true"/> only when the implementation knows no configured middleware applies.
	/// The default is <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// The direct-local path invokes the handler without running the pipeline, so nothing configured
	/// there takes effect on it -- no validation, no authorization, no tenant identity -- and failures
	/// are returned as a result rather than thrown. That is a sound optimisation when nothing is
	/// configured and a silent hole when something is, and only the dispatcher can tell the difference.
	/// <para>
	/// Defaults to <see langword="false"/> so an implementation that does not answer gets the pipeline.
	/// A needless microsecond is a better default than a configured stage that did not run. Override it
	/// to opt into the fast path when you can determine the skip is unobservable.
	/// </para>
	/// </remarks>
	bool CanBypassMiddlewareFor(Type messageType) => false;

	/// <summary>
	/// Dispatches a local action without materializing <see cref="IMessageResult"/> on the hot path.
	/// </summary>
	/// <remarks>
	/// Bypasses routing and the middleware pipeline (validation, authorization, telemetry). Use only for
	/// message types with no such cross-cutting requirements; otherwise use
	/// <see cref="IDispatcher"/>.DispatchAsync.
	/// </remarks>
	/// <typeparam name="TMessage">Type of action.</typeparam>
	/// <param name="message">The message instance.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A value task representing completion.</returns>
	ValueTask DispatchLocalAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
		where TMessage : IDispatchAction;

	/// <summary>
	/// Dispatches a local action with response without materializing <see cref="IMessageResult{TResponse}"/> on the hot path.
	/// </summary>
	/// <remarks>
	/// Bypasses routing and the middleware pipeline (validation, authorization, telemetry). Use only for
	/// message types with no such cross-cutting requirements; otherwise use
	/// <see cref="IDispatcher"/>.DispatchAsync.
	/// </remarks>
	/// <typeparam name="TMessage">Type of action.</typeparam>
	/// <typeparam name="TResponse">Type of response.</typeparam>
	/// <param name="message">The message instance.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A value task containing the handler response.</returns>
	ValueTask<TResponse?> DispatchLocalAsync<TMessage, TResponse>(TMessage message, CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>;
}
