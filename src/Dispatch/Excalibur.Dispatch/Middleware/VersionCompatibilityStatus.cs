// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Version compatibility status.
/// </summary>
public enum VersionCompatibilityStatus
{
	/// <summary>
	/// Version is compatible.
	/// </summary>
	Compatible = 0,

	/// <summary>
	/// Version is deprecated but still supported.
	/// </summary>
	Deprecated = 1,

	/// <summary>
	/// Version is incompatible.
	/// </summary>
	Incompatible = 2,

	/// <summary>
	/// Version status is unknown.
	/// </summary>
	Unknown = 3,
}
