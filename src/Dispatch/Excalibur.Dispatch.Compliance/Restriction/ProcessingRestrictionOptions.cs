// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Restriction;

/// <summary>
/// Configuration options for the processing restriction service.
/// </summary>
public sealed class ProcessingRestrictionOptions
{
	/// <summary>
	/// Gets or sets the default duration for processing restrictions.
	/// </summary>
	/// <value>The default restriction duration. Default is 30 days.</value>
	/// <remarks>
	/// This is the default period for which data processing is restricted.
	/// After this period, the restriction should be reviewed and either
	/// extended or removed.
	/// </remarks>
	public TimeSpan DefaultRestrictionDuration { get; set; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets or sets a value indicating whether to notify downstream
	/// systems when a restriction is applied or removed.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to send notifications on restriction changes;
	/// otherwise, <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	public bool NotifyOnRestriction { get; set; } = true;
}
