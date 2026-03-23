// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Error severity levels for batch processing operations.
/// </summary>
/// <remarks>
/// Renamed from <c>ErrorSeverity</c> to avoid collision with
/// <c>Excalibur.Dispatch.Exceptions.ErrorSeverity</c> which includes
/// additional levels (Information, Fatal) for dispatch exception handling.
/// </remarks>
public enum BatchErrorSeverity
{
	/// <summary>
	/// Informational.
	/// </summary>
	Info = 0,

	/// <summary>
	/// Warning.
	/// </summary>
	Warning = 1,

	/// <summary>
	/// Error.
	/// </summary>
	Error = 2,

	/// <summary>
	/// Critical error.
	/// </summary>
	Critical = 3,
}
