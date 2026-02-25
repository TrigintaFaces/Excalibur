// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Defines the contract for encryption version migration services.
/// </summary>
/// <remarks>
/// <para>
/// This service handles the migration of encrypted data between encryption versions.
/// It supports both lazy re-encryption (on-access) and batch migration strategies.
/// </para>
/// <para>
/// Version detection is performed automatically when reading encrypted data,
/// and re-encryption happens transparently if the data version is older than
/// the current target version.
/// </para>
/// </remarks>
public interface IMigrationService
{
	/// <summary>
	/// Gets the current encryption format version.
	/// </summary>
	EncryptionVersion CurrentVersion { get; }

	/// <summary>
	/// Detects the encryption version of the provided ciphertext.
	/// </summary>
	/// <param name="ciphertext">The encrypted data to analyze.</param>
	/// <returns>The detected encryption version.</returns>
	EncryptionVersion DetectVersion(ReadOnlySpan<byte> ciphertext);

	/// <summary>
	/// Determines if the ciphertext requires migration to a newer version.
	/// </summary>
	/// <param name="ciphertext">The encrypted data to check.</param>
	/// <returns>True if migration is required; otherwise, false.</returns>
	bool RequiresMigration(ReadOnlySpan<byte> ciphertext);

	/// <summary>
	/// Migrates encrypted data from an older version to the current version.
	/// </summary>
	/// <param name="ciphertext">The encrypted data to migrate.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The migration result containing the new ciphertext.</returns>
	Task<VersionMigrationResult> MigrateAsync(byte[] ciphertext, CancellationToken cancellationToken);

	/// <summary>
	/// Performs batch migration of multiple encrypted items.
	/// </summary>
	/// <param name="items">The items to migrate.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The batch migration result.</returns>
	Task<VersionBatchMigrationResult> MigrateBatchAsync(
		IEnumerable<MigrationItem> items,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current migration progress statistics.
	/// </summary>
	/// <returns>The migration progress information.</returns>
	VersionMigrationProgress GetProgress();
}

/// <summary>
/// Represents an encryption format version.
/// </summary>
/// <param name="Major">The major version number.</param>
/// <param name="Minor">The minor version number.</param>
public readonly record struct EncryptionVersion(int Major, int Minor) : IComparable<EncryptionVersion>
{
	/// <summary>
	/// Version 1.0 - Initial AES-256-GCM format.
	/// </summary>
	public static readonly EncryptionVersion Version10 = new(1, 0);

	/// <summary>
	/// Version 1.1 - Enhanced metadata and key versioning.
	/// </summary>
	public static readonly EncryptionVersion Version11 = new(1, 1);

	/// <summary>
	/// Unknown or undetectable version.
	/// </summary>
	public static readonly EncryptionVersion Unknown = new(0, 0);

	/// <inheritdoc />
	public int CompareTo(EncryptionVersion other)
	{
		var majorComparison = Major.CompareTo(other.Major);
		return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
	}

	/// <inheritdoc />
	public override string ToString() => $"v{Major}.{Minor}";

	/// <summary>
	/// Parses a version string into an EncryptionVersion.
	/// </summary>
	/// <param name="version">The version string (e.g., "v1.0" or "1.0").</param>
	/// <returns>The parsed encryption version.</returns>
	public static EncryptionVersion Parse(string version)
	{
		if (string.IsNullOrEmpty(version))
		{
			return Unknown;
		}

		var v = version.StartsWith('v') || version.StartsWith('V') ? version[1..] : version;
		var parts = v.Split('.');

		if (parts.Length >= 2 &&
			int.TryParse(parts[0], out var major) &&
			int.TryParse(parts[1], out var minor))
		{
			return new EncryptionVersion(major, minor);
		}

		return Unknown;
	}

	/// <summary>
	/// Determines if this version is greater than another version.
	/// </summary>
	public static bool operator >(EncryptionVersion left, EncryptionVersion right) => left.CompareTo(right) > 0;

	/// <summary>
	/// Determines if this version is less than another version.
	/// </summary>
	public static bool operator <(EncryptionVersion left, EncryptionVersion right) => left.CompareTo(right) < 0;

	/// <summary>
	/// Determines if this version is greater than or equal to another version.
	/// </summary>
	public static bool operator >=(EncryptionVersion left, EncryptionVersion right) => left.CompareTo(right) >= 0;

	/// <summary>
	/// Determines if this version is less than or equal to another version.
	/// </summary>
	public static bool operator <=(EncryptionVersion left, EncryptionVersion right) => left.CompareTo(right) <= 0;
}

/// <summary>
/// Represents the result of a single migration operation.
/// </summary>
/// <param name="OriginalCiphertext">The original ciphertext before migration.</param>
/// <param name="MigratedCiphertext">The migrated ciphertext (null if migration failed).</param>
/// <param name="SourceVersion">The source encryption version.</param>
/// <param name="TargetVersion">The target encryption version.</param>
/// <param name="Success">Whether the migration succeeded.</param>
/// <param name="ErrorMessage">The error message if migration failed.</param>
/// <param name="Duration">The duration of the migration operation.</param>
public sealed record VersionMigrationResult(
	byte[] OriginalCiphertext,
	byte[]? MigratedCiphertext,
	EncryptionVersion SourceVersion,
	EncryptionVersion TargetVersion,
	bool Success,
	string? ErrorMessage,
	TimeSpan Duration)
{
	/// <summary>
	/// Creates a successful migration result.
	/// </summary>
	public static VersionMigrationResult Succeeded(
		byte[] original,
		byte[] migrated,
		EncryptionVersion source,
		EncryptionVersion target,
		TimeSpan duration) => new(original, migrated, source, target, true, null, duration);

	/// <summary>
	/// Creates a failed migration result.
	/// </summary>
	public static VersionMigrationResult Failed(
		byte[] original,
		EncryptionVersion source,
		EncryptionVersion target,
		string error,
		TimeSpan duration) => new(original, null, source, target, false, error, duration);

	/// <summary>
	/// Creates a "no migration needed" result.
	/// </summary>
	public static VersionMigrationResult NotRequired(byte[] original, EncryptionVersion version) =>
		new(original, original, version, version, true, null, TimeSpan.Zero);
}

/// <summary>
/// Represents an item to be migrated in a batch operation.
/// </summary>
/// <param name="Id">A unique identifier for the item.</param>
/// <param name="Ciphertext">The encrypted data to migrate.</param>
public sealed record MigrationItem(string Id, byte[] Ciphertext);

/// <summary>
/// Represents the result of a batch migration operation.
/// </summary>
/// <param name="TotalItems">The total number of items in the batch.</param>
/// <param name="SuccessCount">The number of successfully migrated items.</param>
/// <param name="FailureCount">The number of failed migrations.</param>
/// <param name="SkippedCount">The number of items that didn't require migration.</param>
/// <param name="Results">The individual migration results.</param>
/// <param name="TotalDuration">The total duration of the batch operation.</param>
public sealed record VersionBatchMigrationResult(
	int TotalItems,
	int SuccessCount,
	int FailureCount,
	int SkippedCount,
	IReadOnlyList<(string Id, VersionMigrationResult Result)> Results,
	TimeSpan TotalDuration);

/// <summary>
/// Represents the current migration progress statistics.
/// </summary>
/// <param name="TotalItemsDetected">The total number of items detected for migration.</param>
/// <param name="ItemsMigrated">The number of items migrated.</param>
/// <param name="FailureCount">The number of migration failures.</param>
/// <param name="VersionDistribution">The breakdown of items by version.</param>
/// <param name="LastUpdated">The timestamp when progress was last updated.</param>
public sealed record VersionMigrationProgress(
	long TotalItemsDetected,
	long ItemsMigrated,
	long FailureCount,
	IReadOnlyDictionary<EncryptionVersion, long> VersionDistribution,
	DateTimeOffset LastUpdated)
{
	/// <summary>
	/// Gets the percentage of migration completion (0-100).
	/// </summary>
	public double CompletionPercentage => TotalItemsDetected > 0
		? (double)ItemsMigrated / TotalItemsDetected * 100
		: 100;
}
