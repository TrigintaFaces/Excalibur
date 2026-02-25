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
	public required byte[] Data { get; init; }

	/// <inheritdoc />
	public required string AggregateType { get; init; }

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Creates a new <see cref="Snapshot"/> with a generated identifier and current timestamp.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="version">The aggregate version.</param>
	/// <param name="data">The serialized state data.</param>
	/// <param name="aggregateType">The type of the aggregate.</param>
	/// <param name="metadata">Optional metadata.</param>
	/// <returns>A new <see cref="Snapshot"/> instance.</returns>
	public static Snapshot Create(
		string aggregateId,
		long version,
		byte[] data,
		string aggregateType,
		IDictionary<string, object>? metadata = null) =>
		new()
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			Version = version,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = data,
			AggregateType = aggregateType,
			Metadata = metadata
		};
}
