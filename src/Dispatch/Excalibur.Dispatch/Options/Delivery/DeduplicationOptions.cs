// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Configuration options for message deduplication.
/// </summary>
public sealed class DeduplicationOptions
{
	/// <summary>
	/// Gets or sets the default expiry time for deduplication entries.
	/// </summary>
	/// <value>
	/// The default expiry time for deduplication entries.
	/// </value>
	public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets a value indicating whether deduplication is enabled.
	/// </summary>
	/// <value>The current <see cref="Enabled"/> value.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the strategy to use for deduplication.
	/// </summary>
	/// <value>The current <see cref="Strategy"/> value.</value>
	public DeduplicationStrategy Strategy { get; set; } = DeduplicationStrategy.MessageId;

	/// <summary>
	/// Gets or sets the maximum number of entries to keep in memory.
	/// </summary>
	/// <value>The current <see cref="MaxMemoryEntries"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxMemoryEntries { get; set; } = 10000;

	/// <summary>
	/// Gets or sets the interval for cleaning up expired entries.
	/// </summary>
	/// <value>
	/// The interval for cleaning up expired entries.
	/// </value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the time window for deduplication.
	/// </summary>
	/// <value>
	/// The time window for deduplication.
	/// </value>
	public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(5);
}
