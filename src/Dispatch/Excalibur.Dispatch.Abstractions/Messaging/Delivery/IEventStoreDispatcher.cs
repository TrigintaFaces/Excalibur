// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Minimal interface for dispatching event store messages.
/// </summary>
public interface IEventStoreDispatcher
{
	/// <summary>
	/// Initializes the dispatcher with the specified dispatcher identifier.
	/// </summary>
	/// <param name="dispatcherId"> The unique identifier for this dispatcher instance. </param>
	void Init(string dispatcherId);

	/// <summary>
	/// Dispatches events from the event store.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous dispatch operation. </returns>
	Task DispatchAsync(CancellationToken cancellationToken);
}
