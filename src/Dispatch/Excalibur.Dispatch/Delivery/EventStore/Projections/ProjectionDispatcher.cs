// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of projection dispatcher that routes events to registered handlers.
/// </summary>
/// <param name="registry"> The registry to resolve projection handlers. </param>
public sealed class ProjectionDispatcher(IProjectionEventHandlerRegistry registry) : IProjectionDispatcher
{
	/// <summary>
	/// Dispatches an event store message to the appropriate projection handler.
	/// </summary>
	/// <typeparam name="TKey"> The type of the aggregate key. </typeparam>
	/// <param name="message"> The event store message to dispatch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous dispatch operation. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when no handler is found for the event type. </exception>
	public async Task DispatchAsync<TKey>(IEventStoreMessage<TKey> message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var handler = registry.ResolveHandler<TKey>(message.EventType);

		if (handler != null)
		{
			await handler.DispatchAsync(message, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			throw new InvalidOperationException(
				$"No handler found for event type {message.EventType}.");
		}
	}
}
