// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Alerting;

/// <summary>
/// Configuration options for the audit alerting service.
/// </summary>
public sealed class AuditAlertOptions
{
	/// <summary>
	/// Gets or sets the evaluation mode for alert rules.
	/// </summary>
	public EvaluationMode EvaluationMode { get; set; } = EvaluationMode.RealTime;

	/// <summary>
	/// Gets or sets the maximum number of alerts that can be generated per minute.
	/// </summary>
	/// <remarks>
	/// Used to prevent alert storms. Default is 100 alerts per minute.
	/// </remarks>
	public int MaxAlertsPerMinute { get; set; } = 100;
}
