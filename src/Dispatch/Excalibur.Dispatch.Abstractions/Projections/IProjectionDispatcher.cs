// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for dispatching event store messages to projection handlers.
/// </summary>
public interface IProjectionDispatcher
{
	/// <summary>
	/// Dispatches an event store message to the appropriate projection handlers.
	/// </summary>
	/// <typeparam name="TKey"> The type of the aggregate key. </typeparam>
	/// <param name="message"> The event store message to dispatch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous dispatch operation. </returns>
	Task DispatchAsync<TKey>(IEventStoreMessage<TKey> message, CancellationToken cancellationToken);
}
