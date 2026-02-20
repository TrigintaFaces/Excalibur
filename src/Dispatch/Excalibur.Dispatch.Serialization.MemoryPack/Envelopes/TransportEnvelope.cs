// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MemoryPack;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// Internal envelope for transport layer wire format.
/// </summary>
/// <remarks>
/// <para>
/// This envelope wraps messages for transport between services.
/// Uses [MemoryPackOrder] for explicit field ordering to support schema evolution.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class TransportEnvelope
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
	/// Gets or sets the timestamp when the message was sent.
	/// </summary>
	[MemoryPackOrder(3)]
	public required DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets or sets the source transport name.
	/// </summary>
	[MemoryPackOrder(4)]
	public string? SourceTransport { get; init; }

	/// <summary>
	/// Gets or sets the target transport name.
	/// </summary>
	[MemoryPackOrder(5)]
	public string? TargetTransport { get; init; }

	/// <summary>
	/// Gets or sets optional transport headers.
	/// </summary>
	[MemoryPackOrder(6)]
	public Dictionary<string, string>? Headers { get; init; }

	/// <summary>
	/// Gets or sets the correlation identifier for distributed tracing.
	/// </summary>
	[MemoryPackOrder(7)]
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets or sets the causation identifier.
	/// </summary>
	[MemoryPackOrder(8)]
	public string? CausationId { get; init; }

	/// <summary>
	/// Gets or sets the schema version for migration detection.
	/// </summary>
	[MemoryPackOrder(9)]
	public int SchemaVersion { get; init; } = 1;
}
