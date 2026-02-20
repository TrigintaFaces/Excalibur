// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Handlers;

/// <summary>
/// Handles the scenario when a message arrives for a saga instance that does not exist.
/// </summary>
/// <typeparam name="TSaga">The type of saga that was not found.</typeparam>
/// <remarks>
/// <para>
/// This interface is invoked by the saga infrastructure when a correlated message
/// cannot be matched to an existing saga instance. Implementations can decide
/// whether to create a new saga, log the event, or take other corrective action.
/// </para>
/// <para>
/// Register a custom implementation via DI to override the default
/// <see cref="LoggingNotFoundHandler{TSaga}"/> behavior.
/// </para>
/// </remarks>
public interface ISagaNotFoundHandler<TSaga>
	where TSaga : class
{
	/// <summary>
	/// Handles a message that could not be correlated to an existing saga instance.
	/// </summary>
	/// <param name="message">The message that was received.</param>
	/// <param name="sagaId">The saga identifier that was looked up but not found.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous handling operation.</returns>
	Task HandleAsync(object message, string sagaId, CancellationToken cancellationToken);
}
