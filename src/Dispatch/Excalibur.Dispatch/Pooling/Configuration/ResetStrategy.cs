// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Pooling.Configuration;

/// <summary>
/// Message reset strategy.
/// </summary>
public enum ResetStrategy
{
	/// <summary>
	/// Automatically determine best strategy.
	/// </summary>
	Auto = 0,

	/// <summary>
	/// Use source-generated reset if available.
	/// </summary>
	SourceGenerated = 1,

	/// <summary>
	/// Use IPoolable interface.
	/// </summary>
	Interface = 2,

	/// <summary>
	/// No reset - only pool stateless objects.
	/// </summary>
	None = 3,

	/// <summary>
	/// Always create new instances.
	/// </summary>
	Disabled = 4,
}
