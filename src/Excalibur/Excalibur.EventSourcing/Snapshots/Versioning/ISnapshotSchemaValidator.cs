// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Snapshots.Versioning;

/// <summary>
/// Validates snapshot schema version compatibility.
/// </summary>
/// <remarks>
/// <para>
/// Implementations check whether a snapshot's stored schema version is compatible
/// with the current code version. If incompatible, the snapshot should be discarded
/// and the aggregate rehydrated from events.
/// </para>
/// </remarks>
public interface ISnapshotSchemaValidator
{
	/// <summary>
	/// Validates whether the stored schema version is compatible with the current version.
	/// </summary>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="storedVersion">The schema version stored in the snapshot.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A validation result indicating compatibility.</returns>
	ValueTask<SnapshotSchemaValidationResult> ValidateAsync(
		string aggregateType,
		int storedVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current schema version for the specified aggregate type.
	/// </summary>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <returns>The current schema version, or <see langword="null"/> if no version is registered.</returns>
	int? GetCurrentVersion(string aggregateType);
}

/// <summary>
/// Represents the result of a snapshot schema validation.
/// </summary>
/// <param name="IsCompatible">Whether the stored snapshot is compatible with the current schema.</param>
/// <param name="StoredVersion">The schema version found in the stored snapshot.</param>
/// <param name="CurrentVersion">The current expected schema version.</param>
/// <param name="Reason">An optional reason when the snapshot is not compatible.</param>
public sealed record SnapshotSchemaValidationResult(
	bool IsCompatible,
	int StoredVersion,
	int CurrentVersion,
	string? Reason = null)
{
	/// <summary>
	/// Creates a compatible validation result.
	/// </summary>
	/// <param name="version">The schema version.</param>
	/// <returns>A compatible result.</returns>
	public static SnapshotSchemaValidationResult Compatible(int version) =>
		new(true, version, version);

	/// <summary>
	/// Creates an incompatible validation result.
	/// </summary>
	/// <param name="storedVersion">The stored schema version.</param>
	/// <param name="currentVersion">The current expected schema version.</param>
	/// <param name="reason">The reason for incompatibility.</param>
	/// <returns>An incompatible result.</returns>
	public static SnapshotSchemaValidationResult Incompatible(
		int storedVersion,
		int currentVersion,
		string? reason = null) =>
		new(false, storedVersion, currentVersion, reason);
}
