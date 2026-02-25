// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MemoryPack;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// Internal envelope for outbox messages in binary wire format.
/// </summary>
/// <remarks>
/// <para>
/// This envelope wraps outbox messages for persistence and transport.
/// Uses [MemoryPackOrder] for explicit field ordering to support schema evolution.
/// </para>
/// <para>
/// Versioning strategy: New optional fields added at the end with higher order numbers.
/// Existing field orders must never change.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class OutboxEnvelope
{
	/// <summary>
	/// Gets or sets the unique message identifier.
	/// </summary>
	[MemoryPackOrder(0)]
	public required Guid MessageId { get; init; }

	/// <summary>
	/// Gets or sets the fully qualified message type name.
	/// </summary>
	[MemoryPackOrder(1)]
	public required string MessageType { get; init; }

	/// <summary>
	/// Gets or sets the serialized message payload.
	/// </summary>
	[MemoryPackOrder(2)]
	public required byte[] Payload { get; init; }

	/// <summary>
	/// Gets or sets the timestamp when the message was created.
	/// </summary>
	[MemoryPackOrder(3)]
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets or sets optional message headers.
	/// </summary>
	[MemoryPackOrder(4)]
	public Dictionary<string, string>? Headers { get; init; }

	/// <summary>
	/// Gets or sets the correlation identifier for distributed tracing.
	/// </summary>
	[MemoryPackOrder(5)]
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets or sets the causation identifier for event sourcing.
	/// </summary>
	[MemoryPackOrder(6)]
	public string? CausationId { get; init; }

	/// <summary>
	/// Gets or sets the schema version for migration detection.
	/// </summary>
	/// <value>Defaults to 1 for the initial schema version.</value>
	[MemoryPackOrder(7)]
	public int SchemaVersion { get; init; } = 1;
}
