// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the result of logging an audit event, containing the assigned event ID and hash.
/// </summary>
public readonly record struct AuditEventId
{
	/// <summary>
	/// Gets the unique identifier assigned to the audit event.
	/// </summary>
	public required string EventId { get; init; }

	/// <summary>
	/// Gets the cryptographic hash of the audit event.
	/// </summary>
	public required string EventHash { get; init; }

	/// <summary>
	/// Gets the sequence number of the event in the audit chain.
	/// </summary>
	public required long SequenceNumber { get; init; }

	/// <summary>
	/// Gets the timestamp when the event was recorded.
	/// </summary>
	public required DateTimeOffset RecordedAt { get; init; }
}
