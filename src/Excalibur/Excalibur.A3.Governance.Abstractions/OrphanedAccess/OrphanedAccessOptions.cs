// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>
/// Configuration options for orphaned access detection.
/// </summary>
public sealed class OrphanedAccessOptions
{
	/// <summary>
	/// Gets or sets the scan interval in hours.
	/// </summary>
	/// <value>Defaults to 24 hours.</value>
	[Range(1, 8760)]
	public int ScanIntervalHours { get; set; } = 24;

	/// <summary>
	/// Gets or sets the grace period in days before an inactive user's grants are recommended for revocation.
	/// </summary>
	/// <value>Defaults to 30 days.</value>
	[Range(1, 365)]
	public int InactiveGracePeriodDays { get; set; } = 30;

	/// <summary>
	/// Gets or sets whether to automatically recommend revocation for departed users.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	public bool AutoRevokeDeparted { get; set; }

	/// <summary>
	/// Gets or sets whether to automatically recommend revocation for inactive users past the grace period.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	public bool AutoRevokeAfterGracePeriod { get; set; }
}
