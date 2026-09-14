// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model;

/// <summary>
/// Default implementation of <see cref="ISnapshot"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides a record-based immutable implementation of the snapshot interface,
/// suitable for most event sourcing scenarios.
/// </para>
/// </remarks>
public sealed record Snapshot : ISnapshot
{
	/// <inheritdoc />
	public required string SnapshotId { get; init; }

	/// <inheritdoc />
	public required string AggregateId { get; init; }

	/// <inheritdoc />
	public required long Version { get; init; }

	/// <inheritdoc />
	public required DateTimeOffset CreatedAt { get; init; }

	/// <inheritdoc />
	public required ReadOnlyMemory<byte> Data { get; init; }

	/// <inheritdoc />
	public required string AggregateType { get; init; }

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; init; }

	/// <inheritdoc/>
	/// <remarks>
	/// Optional so a single-tenant host constructs a snapshot exactly as before. A multi-tenant host sets it,
	/// and the store persists it as part of the snapshot's identity rather than as loose metadata.
	/// </remarks>
	public string? TenantId { get; init; }

	/// <summary>
	/// Creates a new <see cref="Snapshot"/> with a generated identifier and current timestamp.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="version">The aggregate version.</param>
	/// <param name="data">The serialized state data as an immutable memory region.</param>
	/// <param name="aggregateType">The type of the aggregate.</param>
	/// <param name="metadata">Optional metadata.</param>
	/// <returns>A new <see cref="Snapshot"/> instance.</returns>
	public static Snapshot Create(
		string aggregateId,
		long version,
		byte[] data,
		string aggregateType,
		IDictionary<string, object>? metadata = null) =>
		Create(aggregateId, version, data, aggregateType, TimeProvider.System, Guid.NewGuid, metadata);

	/// <summary>
	/// Testable overload: the clock and id generator are injected so a caller (a deterministic test, or a
	/// future feature needing a non-random snapshot id) can control them. Production callers use the
	/// five-argument overload, which supplies <see cref="TimeProvider.System"/> and <see cref="Guid.NewGuid()"/>.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="version">The aggregate version.</param>
	/// <param name="data">The serialized state data as an immutable memory region.</param>
	/// <param name="aggregateType">The type of the aggregate.</param>
	/// <param name="timeProvider">The clock used to stamp <see cref="CreatedAt"/>.</param>
	/// <param name="idGenerator">Generates the <see cref="SnapshotId"/>.</param>
	/// <param name="metadata">Optional metadata.</param>
	/// <returns>A new <see cref="Snapshot"/> instance.</returns>
	internal static Snapshot Create(
		string aggregateId,
		long version,
		byte[] data,
		string aggregateType,
		TimeProvider timeProvider,
		Func<Guid> idGenerator,
		IDictionary<string, object>? metadata = null) =>
		new()
		{
			SnapshotId = idGenerator().ToString(),
			AggregateId = aggregateId,
			Version = version,
			CreatedAt = timeProvider.GetUtcNow(),
			Data = data,
			AggregateType = aggregateType,
			Metadata = metadata
		};
}
