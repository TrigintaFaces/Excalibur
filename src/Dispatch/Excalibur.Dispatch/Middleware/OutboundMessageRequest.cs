// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Represents a staging request for an outbound message.
/// </summary>
/// <remarks> Creates a new outbound message request. </remarks>
public sealed class OutboundMessageRequest(
	IDispatchMessage message,
	string? destination = null,
	DateTimeOffset? scheduledAt = null)
{
	/// <summary>
	/// Gets the message to be sent.
	/// </summary>
	/// <value>
	/// The message to be sent.
	/// </value>
	public IDispatchMessage Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

	/// <summary>
	/// Gets the destination for the message, if specified.
	/// </summary>
	/// <value>The current <see cref="Destination"/> value.</value>
	public string? Destination { get; } = destination;

	/// <summary>
	/// Gets the scheduled delivery time, if specified.
	/// </summary>
	/// <value>The current <see cref="ScheduledAt"/> value.</value>
	public DateTimeOffset? ScheduledAt { get; } = scheduledAt;
}
