// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Configuration options for message deduplication.
/// </summary>
public sealed class DeduplicationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether deduplication is enabled.
	/// </summary>
	/// <value> Default is false (disabled when inbox is enabled). </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the expiry time in hours for deduplication entries.
	/// </summary>
	/// <value> Default is 24 hours. </value>
	[Range(1, int.MaxValue)]
	public int ExpiryHours { get; set; } = 24;

	/// <summary>
	/// Gets or sets the cleanup interval for expired entries.
	/// </summary>
	/// <value>
	/// The cleanup interval for expired entries.
	/// </value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the deduplication window in seconds.
	/// </summary>
	/// <value>The current <see cref="WindowSeconds"/> value.</value>
	[Range(1, int.MaxValue)]
	public int WindowSeconds { get; set; } = 300;

	/// <summary>
	/// Gets or sets the maximum size of the deduplication cache.
	/// </summary>
	/// <value>The current <see cref="MaxCacheSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxCacheSize { get; set; } = 10000;
}
