// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// General encryption configuration options.
/// </summary>
public sealed class EncryptionOptions
{
	/// <summary>
	/// Gets or sets the default key purpose when not specified in the encryption context.
	/// </summary>
	/// <remarks>
	/// Used for key selection when <see cref="EncryptionContext.Purpose"/> is not specified.
	/// Common purposes include "field-encryption", "document-encryption", "api-key-encryption".
	/// </remarks>
	public string DefaultPurpose { get; set; } = "default";

	/// <summary>
	/// Gets or sets a value indicating whether FIPS 140-2 compliance is required by default.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, encryption operations will fail if the provider or key
	/// is not FIPS 140-2 compliant, unless overridden in the encryption context.
	/// </remarks>
	public bool RequireFipsCompliance { get; set; }

	/// <summary>
	/// Gets or sets the default tenant ID for multi-tenant scenarios.
	/// </summary>
	/// <remarks>
	/// Used when <see cref="EncryptionContext.TenantId"/> is not specified.
	/// Leave <c>null</c> for single-tenant applications.
	/// </remarks>
	public string? DefaultTenantId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include timing metadata in encrypted data.
	/// </summary>
	/// <remarks>
	/// When <c>true</c> (default), encrypted data includes the timestamp when encryption occurred.
	/// This is useful for auditing and key rotation tracking.
	/// </remarks>
	public bool IncludeTimingMetadata { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum age of encrypted data before warnings are logged.
	/// </summary>
	/// <remarks>
	/// When set, a warning is logged during decryption if the data was encrypted
	/// longer ago than this threshold. Helps identify data that needs re-encryption
	/// after key rotation.
	/// </remarks>
	public TimeSpan? EncryptionAgeWarningThreshold { get; set; }

	/// <summary>
	/// Gets or sets the encryption mode for field-level encryption operations.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This property controls the encryption behavior during migration phases.
	/// The default is <see cref="EncryptionMode.EncryptAndDecrypt"/> for normal operation.
	/// </para>
	/// <para>
	/// See <see cref="EncryptionMode"/> for available modes and their use cases.
	/// </para>
	/// </remarks>
	public EncryptionMode Mode { get; set; } = EncryptionMode.EncryptAndDecrypt;

	/// <summary>
	/// Gets or sets a value indicating whether lazy migration is enabled.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When enabled, plaintext data is encrypted opportunistically during
	/// normal read/write operations based on <see cref="LazyMigrationMode"/>.
	/// </para>
	/// <para>
	/// This enables gradual migration without requiring dedicated batch processing.
	/// </para>
	/// </remarks>
	public bool LazyMigrationEnabled { get; set; }

	/// <summary>
	/// Gets or sets when lazy migration encrypts plaintext data.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Only used when <see cref="LazyMigrationEnabled"/> is <c>true</c>.
	/// Default is <see cref="LazyMigrationMode.Both"/> for fastest migration.
	/// </para>
	/// <para>
	/// Use <see cref="LazyMigrationMode.OnRead"/> for read-heavy workloads,
	/// or <see cref="LazyMigrationMode.OnWrite"/> to avoid extra write operations on reads.
	/// </para>
	/// </remarks>
	public LazyMigrationMode LazyMigrationMode { get; set; } = LazyMigrationMode.Both;
}

/// <summary>
/// Configuration options for encryption migration operations.
/// </summary>
public sealed class EncryptionMigrationOptions
{
	/// <summary>
	/// Gets or sets the batch size for migration operations.
	/// </summary>
	/// <remarks>
	/// Larger batch sizes improve throughput but increase memory usage and
	/// transaction scope. Default is 100.
	/// </remarks>
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum degree of parallelism for migration operations.
	/// </summary>
	/// <remarks>
	/// Set to 1 for sequential processing. Higher values improve throughput
	/// but increase load on the key management system. Default is 4.
	/// </remarks>
	public int MaxDegreeOfParallelism { get; set; } = 4;

	/// <summary>
	/// Gets or sets a value indicating whether to skip records that fail migration.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, failed records are logged and skipped, allowing the migration
	/// to continue. When <c>false</c> (default), the first failure stops the migration.
	/// </remarks>
	public bool ContinueOnError { get; set; }

	/// <summary>
	/// Gets or sets the delay between batches.
	/// </summary>
	/// <remarks>
	/// Introduces a delay between processing batches to reduce load on the system.
	/// Default is <see cref="TimeSpan.Zero"/> (no delay).
	/// </remarks>
	public TimeSpan DelayBetweenBatches { get; set; } = TimeSpan.Zero;

	/// <summary>
	/// Gets or sets the source provider ID for migration.
	/// </summary>
	/// <remarks>
	/// The provider that was used to encrypt the data being migrated.
	/// If <c>null</c>, the registry will auto-detect the source provider.
	/// </remarks>
	public string? SourceProviderId { get; set; }

	/// <summary>
	/// Gets or sets the target provider ID for migration.
	/// </summary>
	/// <remarks>
	/// The provider to use for re-encryption. If <c>null</c>, the current
	/// primary provider is used.
	/// </remarks>
	public string? TargetProviderId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to verify decryption before re-encryption.
	/// </summary>
	/// <remarks>
	/// When <c>true</c> (default), each record is decrypted and verified before
	/// re-encryption. This adds overhead but ensures data integrity.
	/// </remarks>
	public bool VerifyBeforeReEncrypt { get; set; } = true;

	/// <summary>
	/// Gets or sets the timeout for individual migration operations.
	/// </summary>
	/// <remarks>
	/// Maximum time allowed for a single record migration. Default is 30 seconds.
	/// </remarks>
	public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
