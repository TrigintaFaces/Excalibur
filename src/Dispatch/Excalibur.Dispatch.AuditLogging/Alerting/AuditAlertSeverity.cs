// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Alerting;

/// <summary>
/// Severity levels for audit alert rules.
/// </summary>
public enum AuditAlertSeverity
{
	/// <summary>Informational alert.</summary>
	Info = 0,

	/// <summary>Warning-level alert.</summary>
	Warning = 1,

	/// <summary>Critical alert requiring immediate attention.</summary>
	Critical = 2
}
