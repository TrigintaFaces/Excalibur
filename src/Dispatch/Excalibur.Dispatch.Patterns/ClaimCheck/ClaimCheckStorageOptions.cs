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
	[Required]
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
	/// Gets or sets the operation and resilience options.
	/// </summary>
	/// <value>The operation options including concurrency, buffering, timeouts, and retries.</value>
	public ClaimCheckOperationOptions Operations { get; set; } = new();
}
