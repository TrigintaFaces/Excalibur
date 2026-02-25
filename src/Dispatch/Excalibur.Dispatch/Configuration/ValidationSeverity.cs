// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Severity levels for validation issues.
/// </summary>
public enum ValidationSeverity
{
	/// <summary>
	/// Informational message.
	/// </summary>
	Info = 0,

	/// <summary>
	/// Warning that should be reviewed.
	/// </summary>
	Warning = 1,

	/// <summary>
	/// Error that prevents synthesis.
	/// </summary>
	Error = 2,
}
