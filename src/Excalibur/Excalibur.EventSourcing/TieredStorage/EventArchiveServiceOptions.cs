// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Configuration options for <see cref="EventArchiveService"/>.
/// </summary>
internal sealed class EventArchiveServiceOptions
{
	/// <summary>
	/// Gets or sets the delay before a new scan round or after a page-fetch failure.
	/// </summary>
	/// <value>Default is 1 hour.</value>
	public TimeSpan ArchiveInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the maximum number of stream identities to examine per page, including ineligible streams.
	/// </summary>
	/// <value>Default is 100.</value>
	public int BatchSize { get; set; } = 100;
}
