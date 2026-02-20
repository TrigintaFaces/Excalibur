// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MemoryPack;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// Internal envelope for inbox messages in binary wire format.
/// </summary>
/// <remarks>
/// <para>
/// This envelope wraps inbox messages for persistence and deduplication.
/// Uses [MemoryPackOrder] for explicit field ordering to support schema evolution.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class InboxEnvelope
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
	/// Gets or sets the timestamp when the message was received.
	/// </summary>
	[MemoryPackOrder(3)]
	public required DateTimeOffset ReceivedAt { get; init; }

	/// <summary>
	/// Gets or sets optional message metadata.
	/// </summary>
	[MemoryPackOrder(4)]
	public Dictionary<string, string>? Metadata { get; init; }

	/// <summary>
	/// Gets or sets the correlation identifier for distributed tracing.
	/// </summary>
	[MemoryPackOrder(5)]
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets or sets the source transport name.
	/// </summary>
	[MemoryPackOrder(6)]
	public string? SourceTransport { get; init; }

	/// <summary>
	/// Gets or sets the schema version for migration detection.
	/// </summary>
	[MemoryPackOrder(7)]
	public int SchemaVersion { get; init; } = 1;
}
