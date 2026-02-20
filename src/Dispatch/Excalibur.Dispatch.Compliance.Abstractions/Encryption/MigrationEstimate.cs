// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents an estimate of the scope and duration of a migration operation.
/// </summary>
public sealed record MigrationEstimate
{
	/// <summary>
	/// Gets the estimated number of items requiring migration.
	/// </summary>
	public required int EstimatedItemCount { get; init; }

	/// <summary>
	/// Gets the estimated total data size in bytes.
	/// </summary>
	public required long EstimatedDataSizeBytes { get; init; }

	/// <summary>
	/// Gets the estimated duration of the migration.
	/// </summary>
	public required TimeSpan EstimatedDuration { get; init; }

	/// <summary>
	/// Gets the breakdown by encryption algorithm.
	/// </summary>
	public IReadOnlyDictionary<EncryptionAlgorithm, int>? ByAlgorithm { get; init; }

	/// <summary>
	/// Gets the breakdown by key ID.
	/// </summary>
	public IReadOnlyDictionary<string, int>? ByKeyId { get; init; }

	/// <summary>
	/// Gets the breakdown by tenant.
	/// </summary>
	public IReadOnlyDictionary<string, int>? ByTenant { get; init; }

	/// <summary>
	/// Gets the oldest encryption timestamp in the scope.
	/// </summary>
	public DateTimeOffset? OldestEncryptedAt { get; init; }

	/// <summary>
	/// Gets the newest encryption timestamp in the scope.
	/// </summary>
	public DateTimeOffset? NewestEncryptedAt { get; init; }

	/// <summary>
	/// Gets any warnings or recommendations for the migration.
	/// </summary>
	public IReadOnlyList<string>? Warnings { get; init; }

	/// <summary>
	/// Gets the timestamp when this estimate was calculated.
	/// </summary>
	public DateTimeOffset EstimatedAt { get; init; } = DateTimeOffset.UtcNow;
}
