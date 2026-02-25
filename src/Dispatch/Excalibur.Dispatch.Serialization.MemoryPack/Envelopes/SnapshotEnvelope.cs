// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MemoryPack;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// Internal envelope for aggregate snapshots in wire format.
/// </summary>
/// <remarks>
/// <para>
/// This envelope wraps aggregate snapshots for persistence.
/// Uses [MemoryPackOrder] for explicit field ordering to support schema evolution.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class SnapshotEnvelope
{
	/// <summary>
	/// Gets or sets the aggregate identifier.
	/// </summary>
	[MemoryPackOrder(0)]
	public required Guid AggregateId { get; init; }

	/// <summary>
	/// Gets or sets the aggregate type name.
	/// </summary>
	[MemoryPackOrder(1)]
	public required string AggregateType { get; init; }

	/// <summary>
	/// Gets or sets the aggregate version at the time of this snapshot.
	/// </summary>
	[MemoryPackOrder(2)]
	public required long Version { get; init; }

	/// <summary>
	/// Gets or sets the serialized aggregate state.
	/// </summary>
	[MemoryPackOrder(3)]
	public required byte[] State { get; init; }

	/// <summary>
	/// Gets or sets the timestamp when the snapshot was created.
	/// </summary>
	[MemoryPackOrder(4)]
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets or sets optional snapshot metadata.
	/// </summary>
	[MemoryPackOrder(5)]
	public Dictionary<string, string>? Metadata { get; init; }

	/// <summary>
	/// Gets or sets the schema version for migration detection.
	/// </summary>
	[MemoryPackOrder(6)]
	public int SchemaVersion { get; init; } = 1;
}
