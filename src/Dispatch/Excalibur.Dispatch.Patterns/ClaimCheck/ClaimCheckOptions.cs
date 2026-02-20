// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Core configuration options for the Claim Check pattern implementation.
/// </summary>
/// <remarks>
/// <para>
/// Storage-specific, compression, and cleanup settings are grouped into focused sub-options:
/// <see cref="Storage"/>, <see cref="Compression"/>, and <see cref="Cleanup"/>.
/// </para>
/// <para>
/// Frequently-used properties are surfaced at the top level for convenience and backward compatibility,
/// delegating to the appropriate sub-options class.
/// </para>
/// </remarks>
public sealed class ClaimCheckOptions
{
	/// <summary>
	/// Gets or sets the threshold in bytes above which payloads should use claim check.
	/// </summary>
	/// <value>The threshold in bytes above which payloads should use claim check.</value>
	[Range(1L, long.MaxValue)]
	public long PayloadThreshold { get; set; } = 256 * 1024; // 256KB default

	/// <summary>
	/// Gets or sets the prefix for claim check identifiers.
	/// </summary>
	/// <value>The prefix for claim check identifiers.</value>
	public string IdPrefix { get; set; } = "cc-";

	/// <summary>
	/// Gets or sets a value indicating whether to validate payload integrity using checksums.
	/// </summary>
	/// <value>A value indicating whether to validate payload integrity using checksums.</value>
	public bool ValidateChecksum { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable basic metrics collection.
	/// </summary>
	/// <value>A value indicating whether to enable basic metrics collection.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets the storage-specific configuration options.
	/// </summary>
	/// <value>The storage-specific configuration options.</value>
	public ClaimCheckStorageOptions Storage { get; } = new();

	/// <summary>
	/// Gets the compression configuration options.
	/// </summary>
	/// <value>The compression configuration options.</value>
	public ClaimCheckCompressionOptions Compression { get; } = new();

	/// <summary>
	/// Gets the cleanup and retention configuration options.
	/// </summary>
	/// <value>The cleanup and retention configuration options.</value>
	public ClaimCheckCleanupOptions Cleanup { get; } = new();

	// ============================================================================
	// Backward-compatible delegating properties.
	// These delegate to sub-options for code that references the flat property names.
	// ============================================================================

	/// <summary>
	/// Gets or sets the connection string for the storage provider.
	/// Delegates to <see cref="Storage"/>.<see cref="ClaimCheckStorageOptions.ConnectionString"/>.
	/// </summary>
	/// <value>The connection string for the storage provider.</value>
	public string ConnectionString
	{
		get => Storage.ConnectionString;
		set => Storage.ConnectionString = value;
	}

	/// <summary>
	/// Gets or sets the container or bucket name for storing payloads.
	/// Delegates to <see cref="Storage"/>.<see cref="ClaimCheckStorageOptions.ContainerName"/>.
	/// </summary>
	/// <value>The container or bucket name for storing payloads.</value>
	public string ContainerName
	{
		get => Storage.ContainerName;
		set => Storage.ContainerName = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether to enable compression for stored payloads.
	/// Delegates to <see cref="Compression"/>.<see cref="ClaimCheckCompressionOptions.EnableCompression"/>.
	/// </summary>
	/// <value>A value indicating whether to enable compression for stored payloads.</value>
	public bool EnableCompression
	{
		get => Compression.EnableCompression;
		set => Compression.EnableCompression = value;
	}

	/// <summary>
	/// Gets or sets the minimum size in bytes for compression to be applied.
	/// Delegates to <see cref="Compression"/>.<see cref="ClaimCheckCompressionOptions.CompressionThreshold"/>.
	/// </summary>
	/// <value>The minimum size in bytes for compression to be applied.</value>
	public long CompressionThreshold
	{
		get => Compression.CompressionThreshold;
		set => Compression.CompressionThreshold = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic cleanup of expired payloads.
	/// Delegates to <see cref="Cleanup"/>.<see cref="ClaimCheckCleanupOptions.EnableCleanup"/>.
	/// </summary>
	/// <value>A value indicating whether to enable automatic cleanup of expired payloads.</value>
	public bool EnableCleanup
	{
		get => Cleanup.EnableCleanup;
		set => Cleanup.EnableCleanup = value;
	}

	/// <summary>
	/// Gets or sets the interval for running cleanup operations.
	/// Delegates to <see cref="Cleanup"/>.<see cref="ClaimCheckCleanupOptions.CleanupInterval"/>.
	/// </summary>
	/// <value>The interval for running cleanup operations.</value>
	public TimeSpan CleanupInterval
	{
		get => Cleanup.CleanupInterval;
		set => Cleanup.CleanupInterval = value;
	}

	/// <summary>
	/// Gets or sets the default retention period for stored payloads.
	/// Delegates to <see cref="Cleanup"/>.<see cref="ClaimCheckCleanupOptions.DefaultTtl"/>.
	/// </summary>
	/// <value>The default retention period for stored payloads.</value>
	public TimeSpan RetentionPeriod
	{
		get => Cleanup.DefaultTtl;
		set => Cleanup.DefaultTtl = value;
	}

	/// <summary>
	/// Gets or sets the default time-to-live for stored payloads.
	/// Delegates to <see cref="Cleanup"/>.<see cref="ClaimCheckCleanupOptions.DefaultTtl"/>.
	/// </summary>
	/// <value>The default time-to-live for stored payloads.</value>
	public TimeSpan DefaultTtl
	{
		get => Cleanup.DefaultTtl;
		set => Cleanup.DefaultTtl = value;
	}

	/// <summary>
	/// Gets or sets the minimum compression ratio (0.0 to 1.0) required to keep compressed data.
	/// Delegates to <see cref="Compression"/>.<see cref="ClaimCheckCompressionOptions.MinCompressionRatio"/>.
	/// </summary>
	/// <value>The minimum compression ratio required to keep compressed data.</value>
	public double MinCompressionRatio
	{
		get => Compression.MinCompressionRatio;
		set => Compression.MinCompressionRatio = value;
	}

	/// <summary>
	/// Gets or sets the compression level to use when compression is enabled.
	/// Delegates to <see cref="Compression"/>.<see cref="ClaimCheckCompressionOptions.CompressionLevel"/>.
	/// </summary>
	/// <value>The compression level to use when compression is enabled.</value>
	public System.IO.Compression.CompressionLevel CompressionLevel
	{
		get => Compression.CompressionLevel;
		set => Compression.CompressionLevel = value;
	}

	/// <summary>
	/// Gets or sets the prefix for blob names in storage.
	/// Delegates to <see cref="Storage"/>.<see cref="ClaimCheckStorageOptions.BlobNamePrefix"/>.
	/// </summary>
	/// <value>The prefix for blob names in storage.</value>
	public string BlobNamePrefix
	{
		get => Storage.BlobNamePrefix;
		set => Storage.BlobNamePrefix = value;
	}
}
