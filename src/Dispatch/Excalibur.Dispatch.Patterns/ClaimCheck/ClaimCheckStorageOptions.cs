// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Storage-specific configuration options for the Claim Check pattern.
/// </summary>
public sealed class ClaimCheckStorageOptions
{
	/// <summary>
	/// Gets or sets the connection string for the storage provider.
	/// </summary>
	/// <value>The connection string for the storage provider.</value>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the container or bucket name for storing payloads.
	/// </summary>
	/// <value>The container or bucket name for storing payloads.</value>
	public string ContainerName { get; set; } = "claim-checks";

	/// <summary>
	/// Gets or sets the prefix for blob names in storage.
	/// </summary>
	/// <value>The prefix for blob names in storage.</value>
	public string BlobNamePrefix { get; set; } = "claims";

	/// <summary>
	/// Gets or sets a value indicating whether to use hierarchical storage (hot/cold tiers).
	/// </summary>
	/// <value>A value indicating whether to use hierarchical storage.</value>
	public bool UseHierarchicalStorage { get; set; }

	/// <summary>
	/// Gets or sets the age threshold for moving payloads to cold storage.
	/// </summary>
	/// <value>The age threshold for moving payloads to cold storage.</value>
	public TimeSpan ColdStorageThreshold { get; set; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets or sets a value indicating whether to enable encryption for stored payloads.
	/// </summary>
	/// <value>A value indicating whether to enable encryption for stored payloads.</value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the chunk size in bytes for large payload processing.
	/// </summary>
	/// <value>The chunk size in bytes for large payload processing.</value>
	[Range(1, int.MaxValue)]
	public int ChunkSize { get; set; } = 1024 * 1024; // 1MB default

	/// <summary>
	/// Gets or sets the maximum number of concurrent operations.
	/// </summary>
	/// <value>The maximum number of concurrent operations.</value>
	[Range(1, int.MaxValue)]
	public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets the size of the buffer pool for reusable buffers.
	/// </summary>
	/// <value>The size of the buffer pool for reusable buffers.</value>
	[Range(1, int.MaxValue)]
	public int BufferPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the timeout for storage operations.
	/// </summary>
	/// <value>The timeout for storage operations.</value>
	public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum number of retries for storage operations.
	/// </summary>
	/// <value>The maximum number of retries for storage operations.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// </summary>
	/// <value>The delay between retry attempts.</value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
