// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines an ultra-local success path for command/query dispatch with minimal overhead.
/// </summary>
/// <remarks>
/// These methods are optimized for local in-process execution and avoid creating an
/// <see cref="IMessageContext"/> unless the dispatch path requires it.
/// </remarks>
public interface IDirectLocalDispatcher
{
	/// <summary>
	/// Dispatches a local action without materializing <see cref="IMessageResult"/> on the hot path.
	/// </summary>
	/// <typeparam name="TMessage">Type of action.</typeparam>
	/// <param name="message">The message instance.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A value task representing completion.</returns>
	ValueTask DispatchLocalAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
		where TMessage : IDispatchAction;

	/// <summary>
	/// Dispatches a local action with response without materializing <see cref="IMessageResult{TResponse}"/> on the hot path.
	/// </summary>
	/// <typeparam name="TMessage">Type of action.</typeparam>
	/// <typeparam name="TResponse">Type of response.</typeparam>
	/// <param name="message">The message instance.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A value task containing the handler response.</returns>
	ValueTask<TResponse?> DispatchLocalAsync<TMessage, TResponse>(TMessage message, CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>;
}
