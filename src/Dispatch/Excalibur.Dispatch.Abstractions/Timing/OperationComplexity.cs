// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the complexity levels for operations, affecting timeout calculations. R7.4: Complexity-based timeout scaling.
/// </summary>
public enum OperationComplexity
{
	/// <summary>
	/// Simple operations that complete quickly.
	/// </summary>
	Simple = 0,

	/// <summary>
	/// Normal operations with standard complexity.
	/// </summary>
	Normal = 1,

	/// <summary>
	/// Complex operations that may take longer to complete.
	/// </summary>
	Complex = 2,

	/// <summary>
	/// Heavy operations that require extended timeouts.
	/// </summary>
	Heavy = 3,
}
