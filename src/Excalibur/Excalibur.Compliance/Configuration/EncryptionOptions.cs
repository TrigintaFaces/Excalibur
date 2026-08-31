// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Compliance.Configuration;

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
	[Required]
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
