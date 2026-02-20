// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for the migration service.
/// </summary>
public sealed class MigrationOptions
{
	/// <summary>
	/// Gets or sets the target encryption version for migrations.
	/// Default is v1.1.
	/// </summary>
	public EncryptionVersion TargetVersion { get; set; } = EncryptionVersion.Version11;

	/// <summary>
	/// Gets or sets a value indicating whether lazy re-encryption is enabled.
	/// When true, data is re-encrypted automatically on read if version is outdated.
	/// Default is true.
	/// </summary>
	public bool EnableLazyReEncryption { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of concurrent migrations in batch operations.
	/// Default is 4.
	/// </summary>
	public int MaxConcurrentMigrations { get; set; } = 4;

	/// <summary>
	/// Gets or sets the batch size for batch migration operations.
	/// Default is 100.
	/// </summary>
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to track migration progress.
	/// Default is true.
	/// </summary>
	public bool TrackProgress { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to fail fast on migration errors.
	/// When false, batch operations continue despite individual failures.
	/// Default is false.
	/// </summary>
	public bool FailFast { get; set; }

	/// <summary>
	/// Gets or sets the timeout for individual migration operations.
	/// Default is 30 seconds.
	/// </summary>
	public TimeSpan MigrationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
