// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.SeparationOfDuties;

/// <summary>
/// Severity level for a Separation of Duties policy or detected conflict.
/// </summary>
public enum SoDSeverity
{
	/// <summary>
	/// Informational conflict that should be logged but not blocked.
	/// </summary>
	Warning = 0,

	/// <summary>
	/// A policy violation that should be blocked by default.
	/// </summary>
	Violation = 1,

	/// <summary>
	/// A critical conflict that must always be blocked and escalated.
	/// </summary>
	Critical = 2,
}
