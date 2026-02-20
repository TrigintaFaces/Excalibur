// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.BoundedContext;

/// <summary>
/// Specifies how bounded context violations are handled.
/// </summary>
public enum BoundedContextEnforcementMode
{
	/// <summary>
	/// Log a warning when a violation is detected but do not throw an exception.
	/// </summary>
	Warn = 0,

	/// <summary>
	/// Throw an <see cref="InvalidOperationException"/> when a violation is detected.
	/// </summary>
	Error = 1,
}
