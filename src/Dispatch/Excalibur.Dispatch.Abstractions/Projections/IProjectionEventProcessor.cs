// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for handling projection events.
/// </summary>
public interface IProjectionEventProcessor : IProjectionHandler
{
	/// <summary>
	/// Handles an event asynchronously.
	/// </summary>
	/// <param name="eventData"> The event data to handle. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task HandleAsync(object eventData, CancellationToken cancellationToken);
}
