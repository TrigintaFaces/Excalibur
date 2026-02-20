// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Defines the severity of a validation issue.
/// </summary>
public enum ValidationSeverity
{
	/// <summary>
	/// Informational - no action required.
	/// </summary>
	Info = 0,

	/// <summary>
	/// Warning - potential issue that should be investigated.
	/// </summary>
	Warning = 1,

	/// <summary>
	/// Error - definite issue that needs to be resolved.
	/// </summary>
	Error = 2,

	/// <summary>
	/// Critical - severe issue that may cause system failure.
	/// </summary>
	Critical = 3,
}
