// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Auditing;

/// <summary>
/// Writes audit events to a durable store or forwarding pipeline. Implementations MUST be provider-neutral at the interface level.
/// </summary>
public interface IAuditSink
{
	/// <summary>
	/// Writes a single audit event.
	/// </summary>
	/// <param name="auditEvent"> The event to persist or forward. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that completes when the event has been written. </returns>
	ValueTask WriteAsync(IAuditEvent auditEvent, CancellationToken cancellationToken);
}
