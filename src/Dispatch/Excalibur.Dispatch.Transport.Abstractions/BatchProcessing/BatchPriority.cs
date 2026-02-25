// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Batch priority levels.
/// </summary>
public enum BatchPriority
{
	/// <summary>
	/// Low priority.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Normal priority.
	/// </summary>
	Normal = 1,

	/// <summary>
	/// High priority.
	/// </summary>
	High = 2,

	/// <summary>
	/// Critical priority.
	/// </summary>
	Critical = 3,
}
