// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Contract for persistent storage of security events.
/// </summary>
public interface ISecurityEventStore
{
	/// <summary>
	/// Stores security events persistently.
	/// </summary>
	/// <param name="events">The events to persist.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when the events are stored.</returns>
	Task StoreEventsAsync(IEnumerable<SecurityEvent> events, CancellationToken cancellationToken);

	/// <summary>
	/// Queries stored security events.
	/// </summary>
	/// <param name="query">The query to execute against the event store.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the matching security events.</returns>
	Task<IEnumerable<SecurityEvent>> QueryEventsAsync(
		SecurityEventQuery query,
		CancellationToken cancellationToken);
}
