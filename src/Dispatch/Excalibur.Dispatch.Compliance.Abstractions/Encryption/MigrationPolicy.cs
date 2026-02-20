// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Defines the policy for determining when encrypted data requires migration.
/// </summary>
public sealed record MigrationPolicy
{
	/// <summary>
	/// Gets or sets the maximum age of keys before migration is required.
	/// </summary>
	public TimeSpan? MaxKeyAge { get; init; }

	/// <summary>
	/// Gets or sets the minimum key version required (data with older versions requires migration).
	/// </summary>
	public int? MinKeyVersion { get; init; }

	/// <summary>
	/// Gets or sets the target encryption algorithm (data using other algorithms requires migration).
	/// </summary>
	public EncryptionAlgorithm? TargetAlgorithm { get; init; }

	/// <summary>
	/// Gets or sets the deprecated algorithms that require migration.
	/// </summary>
	public IReadOnlySet<EncryptionAlgorithm>? DeprecatedAlgorithms { get; init; }

	/// <summary>
	/// Gets or sets the key IDs that require migration.
	/// </summary>
	public IReadOnlySet<string>? DeprecatedKeyIds { get; init; }

	/// <summary>
	/// Gets or sets a value indicating whether to migrate data encrypted before a specific date.
	/// </summary>
	public DateTimeOffset? EncryptedBefore { get; init; }

	/// <summary>
	/// Gets or sets a value indicating whether FIPS compliance is required.
	/// </summary>
	public bool RequireFipsCompliance { get; init; }

	/// <summary>
	/// Gets or sets tenant IDs to scope the migration to.
	/// </summary>
	public IReadOnlySet<string>? TenantIds { get; init; }

	/// <summary>
	/// Gets the default policy that triggers migration for keys older than 90 days.
	/// </summary>
	public static MigrationPolicy Default => new() { MaxKeyAge = TimeSpan.FromDays(90) };

	/// <summary>
	/// Creates a policy that migrates to a specific algorithm.
	/// </summary>
	/// <param name="algorithm"> The target algorithm. </param>
	/// <returns> A migration policy for algorithm migration. </returns>
	public static MigrationPolicy ForAlgorithm(EncryptionAlgorithm algorithm) =>
		new() { TargetAlgorithm = algorithm };

	/// <summary>
	/// Creates a policy that migrates data using deprecated keys.
	/// </summary>
	/// <param name="keyIds"> The deprecated key IDs. </param>
	/// <returns> A migration policy for key migration. </returns>
	public static MigrationPolicy ForDeprecatedKeys(params string[] keyIds) =>
		new() { DeprecatedKeyIds = new HashSet<string>(keyIds, StringComparer.Ordinal) };
}
