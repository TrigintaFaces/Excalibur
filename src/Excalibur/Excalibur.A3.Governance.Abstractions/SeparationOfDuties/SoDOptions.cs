// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.A3.Governance.SeparationOfDuties;

/// <summary>
/// Configuration options for Separation of Duties enforcement.
/// </summary>
public sealed class SoDOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether preventive SoD enforcement is enabled.
	/// When enabled, grant requests that would create a conflict are blocked.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool EnablePreventiveEnforcement { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether detective SoD scanning is enabled.
	/// When enabled, a background service periodically scans for existing violations.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool EnableDetectiveScanning { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval at which the detective scanning service checks for violations.
	/// </summary>
	/// <value>Defaults to 24 hours.</value>
	[Range(typeof(TimeSpan), "00:01:00", "168.00:00:00",
		ErrorMessage = "DetectiveScanInterval must be between 1 minute and 7 days.")]
	public TimeSpan DetectiveScanInterval { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets the minimum severity at which preventive enforcement blocks a grant request.
	/// Conflicts below this severity are logged but allowed.
	/// </summary>
	/// <value>Defaults to <see cref="SoDSeverity.Violation"/>.</value>
	public SoDSeverity MinimumEnforcementSeverity { get; set; } = SoDSeverity.Violation;
}
