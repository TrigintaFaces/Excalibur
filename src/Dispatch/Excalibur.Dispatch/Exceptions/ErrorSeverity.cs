// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Defines the severity level of errors for prioritization and alerting.
/// </summary>
public enum ErrorSeverity
{
	/// <summary>
	/// Informational message, not an error.
	/// </summary>
	Information = 0,

	/// <summary>
	/// Warning that should be investigated but doesn't prevent operation.
	/// </summary>
	Warning = 1,

	/// <summary>
	/// Error that prevents normal operation but system can continue.
	/// </summary>
	Error = 2,

	/// <summary>
	/// Critical error requiring immediate attention.
	/// </summary>
	Critical = 3,

	/// <summary>
	/// Fatal error that will cause system shutdown or major failure.
	/// </summary>
	Fatal = 4,
}
