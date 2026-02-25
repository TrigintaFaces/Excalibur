// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Configuration options for the in-memory deduplicator.
/// </summary>
public sealed class InMemoryDeduplicatorOptions
{
	/// <summary>
	/// Gets or sets the maximum number of entries to keep in memory.
	/// </summary>
	/// <value> Default is 100,000. Set to 0 for unlimited. </value>
	[Range(0, int.MaxValue)]
	public int MaxEntries { get; set; } = 100_000;

	/// <summary>
	/// Gets or sets the default expiry time for entries.
	/// </summary>
	/// <value> Default is 24 hours. </value>
	public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic cleanup of expired entries.
	/// </summary>
	/// <value> Default is true. </value>
	public bool EnableAutomaticCleanup { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval between automatic cleanup runs.
	/// </summary>
	/// <value> Default is 30 minutes. </value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);
}
