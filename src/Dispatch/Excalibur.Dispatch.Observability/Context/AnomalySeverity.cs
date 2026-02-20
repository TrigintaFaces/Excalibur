// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Severity levels for anomalies.
/// </summary>
public enum AnomalySeverity
{
	/// <summary>
	/// Low severity.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Medium severity.
	/// </summary>
	Medium = 1,

	/// <summary>
	/// High severity.
	/// </summary>
	High = 2,
}
