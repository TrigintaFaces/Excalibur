// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Extension methods for projection handlers to simplify event store message processing and dispatching.
/// </summary>
public static class ProjectionHandlerExtensions
{
	/// <summary>
	/// Dispatches an event store message to the appropriate projection handler for processing.
	/// </summary>
	/// <typeparam name="TKey"> The type of the event store message key. </typeparam>
	/// <param name="handler"> The projection handler to dispatch the message to. </param>
	/// <param name="message"> The event store message to process. </param>
	/// <param name="cancellationToken"> Cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous dispatch operation. </returns>
	public static async Task DispatchAsync<TKey>(this IProjectionHandler handler, IEventStoreMessage<TKey> message,
		CancellationToken cancellationToken)
	{
		// Delegate to HandleAsync if DispatchAsync doesn't exist
		if (handler is IProjectionEventProcessor eventHandler)
		{
			await eventHandler.HandleAsync(message.ToEvent(), cancellationToken).ConfigureAwait(false);
		}
	}
}
