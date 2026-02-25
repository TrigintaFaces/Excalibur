// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Snapshot strategy based on aggregate size in memory.
/// </summary>
/// <remarks>
/// <para>
/// Creates a snapshot when the estimated memory size of an aggregate exceeds a threshold.
/// Useful for aggregates with large event payloads or many uncommitted events.
/// </para>
/// </remarks>
public sealed class SizeBasedSnapshotStrategy : ISnapshotStrategy
{
	private readonly long _maxSizeInBytes;

	/// <summary>
	/// Initializes a new instance of the <see cref="SizeBasedSnapshotStrategy"/> class.
	/// </summary>
	/// <param name="maxSizeInBytes">Maximum aggregate size in bytes before creating a snapshot. Default is 1MB.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when max size is zero or negative.</exception>
	public SizeBasedSnapshotStrategy(long maxSizeInBytes = 1_048_576) // 1MB default
	{
		if (maxSizeInBytes <= 0)
		{
			throw new ArgumentOutOfRangeException(
					nameof(maxSizeInBytes),
					Resources.SizeBasedSnapshotStrategy_MaxSizeMustBeGreaterThanZero);
		}

		_maxSizeInBytes = maxSizeInBytes;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public bool ShouldCreateSnapshot(IAggregateRoot aggregate)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		// Estimate aggregate size by serializing it
		var estimatedSize = EstimateAggregateSize(aggregate);
		return estimatedSize > _maxSizeInBytes;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static long EstimateAggregateSize(IAggregateRoot aggregate)
	{
		try
		{
			// Access snapshot support via GetService escape hatch (ISP split)
			if (aggregate.GetService(typeof(IAggregateSnapshotSupport)) is not IAggregateSnapshotSupport snapshotSupport)
			{
				// Aggregate does not support snapshots; fall back to version-based estimate
				return aggregate.Version * 1024;
			}

			// Create a simple representation of the aggregate for size estimation
			var snapshot = snapshotSupport.CreateSnapshot();
			var json = JsonSerializer.Serialize(snapshot);
			return Encoding.UTF8.GetByteCount(json);
		}
		catch
		{
			// If estimation fails, use a conservative approach based on version
			// Assume average event size of 1KB
			return aggregate.Version * 1024;
		}
	}
}
