// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Pooling.Configuration;

/// <summary>
/// Trim behavior under memory pressure.
/// </summary>
public enum TrimBehavior
{
	/// <summary>
	/// No trimming.
	/// </summary>
	None = 0,

	/// <summary>
	/// Trim a fixed percentage.
	/// </summary>
	Fixed = 1,

	/// <summary>
	/// Adaptive trimming based on pressure level.
	/// </summary>
	Adaptive = 2,

	/// <summary>
	/// Aggressive trimming - clear most pools.
	/// </summary>
	Aggressive = 3,
}
