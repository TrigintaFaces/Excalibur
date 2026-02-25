// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Cleanup and retention configuration options for the Claim Check pattern.
/// </summary>
public sealed class ClaimCheckCleanupOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic cleanup of expired payloads.
	/// </summary>
	/// <value>A value indicating whether to enable automatic cleanup of expired payloads.</value>
	public bool EnableCleanup { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval for running cleanup operations.
	/// </summary>
	/// <value>The interval for running cleanup operations.</value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the default time-to-live for stored payloads.
	/// </summary>
	/// <value>The default time-to-live for stored payloads.</value>
	public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the number of items to process in each cleanup batch.
	/// </summary>
	/// <value>The number of items to process in each cleanup batch.</value>
	[Range(1, int.MaxValue)]
	public int CleanupBatchSize { get; set; } = 1000;
}
