// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Defines the validation mode for context validation.
/// </summary>
public enum ValidationMode
{
	/// <summary>
	/// Strict mode - reject messages with invalid context.
	/// </summary>
	Strict = 0,

	/// <summary>
	/// Lenient mode - log warnings but continue processing.
	/// </summary>
	Lenient = 1,
}
