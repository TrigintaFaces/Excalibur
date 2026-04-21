// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Configuration options for <see cref="EventArchiveService"/>.
/// </summary>
internal sealed class EventArchiveServiceOptions
{
	/// <summary>
	/// Gets or sets the interval between archive cycles.
	/// </summary>
	/// <value>Default is 1 hour.</value>
	public TimeSpan ArchiveInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the maximum number of aggregates to process per cycle.
	/// </summary>
	/// <value>Default is 100.</value>
	public int BatchSize { get; set; } = 100;
}
