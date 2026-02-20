// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Defines the severity levels for validation Tests.Shared.Handlers.TestInfrastructure.
/// </summary>
public enum ValidationSeverity
{
	/// <summary>
	/// Information level.
	/// </summary>
	Info = 0,

	/// <summary>
	/// Warning level.
	/// </summary>
	Warning = 1,

	/// <summary>
	/// Error level.
	/// </summary>
	Error = 2,

	/// <summary>
	/// Critical level.
	/// </summary>
	Critical = 3,
}
