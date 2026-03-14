// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

namespace MinimalSample;

/// <summary>
/// Handles the PingEvent. Events can have multiple handlers.
/// </summary>
public sealed class PingEventHandler : IEventHandler<PingEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(PingEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);
		Console.WriteLine($"[PingEventHandler] Event received: {eventMessage.Message}");
		return Task.CompletedTask;
	}
}
