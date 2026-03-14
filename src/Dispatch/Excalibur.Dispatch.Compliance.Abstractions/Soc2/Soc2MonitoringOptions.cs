// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration for SOC 2 continuous monitoring and alerting.
/// </summary>
public sealed class Soc2MonitoringOptions
{
	/// <summary>
	/// Whether to enable continuous compliance monitoring.
	/// Default: true.
	/// </summary>
	public bool EnableContinuousMonitoring { get; set; } = true;

	/// <summary>
	/// Monitoring interval for control validation.
	/// Default: 1 hour.
	/// </summary>
	public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Whether to generate alerts for compliance gaps.
	/// Default: true.
	/// </summary>
	public bool EnableAlerts { get; set; } = true;

	/// <summary>
	/// Alert severity threshold.
	/// Default: Medium.
	/// </summary>
	public GapSeverity AlertThreshold { get; set; } = GapSeverity.Medium;
}
