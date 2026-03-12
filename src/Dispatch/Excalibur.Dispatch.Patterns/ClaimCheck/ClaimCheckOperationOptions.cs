// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Operation and resilience configuration for Claim Check storage.
/// </summary>
public sealed class ClaimCheckOperationOptions
{
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
