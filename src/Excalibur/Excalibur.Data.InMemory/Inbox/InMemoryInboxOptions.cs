// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.InMemory.Inbox;

/// <summary>
/// Configuration options for the in-memory inbox store.
/// </summary>
public sealed class InMemoryInboxOptions
{
	/// <summary>
	/// Gets or sets the maximum number of entries to keep in memory.
	/// </summary>
	/// <value>Default is 10,000. Set to 0 for unlimited.</value>
	[Range(0, int.MaxValue)]
	public int MaxEntries { get; set; } = 10_000;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic cleanup of old entries.
	/// </summary>
	/// <value>Default is true.</value>
	public bool EnableAutomaticCleanup { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval between automatic cleanup runs.
	/// </summary>
	/// <value>Default is 5 minutes.</value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the retention period for processed entries.
	/// </summary>
	/// <value>Default is 7 days.</value>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
