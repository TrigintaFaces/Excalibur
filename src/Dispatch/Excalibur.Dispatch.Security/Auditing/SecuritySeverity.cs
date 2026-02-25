// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Security event severity levels.
/// </summary>
public enum SecuritySeverity
{
	/// <summary>
	/// Represents a low severity security event.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Represents a medium severity security event.
	/// </summary>
	Medium = 1,

	/// <summary>
	/// Represents a high severity security event.
	/// </summary>
	High = 2,

	/// <summary>
	/// Represents a critical severity security event.
	/// </summary>
	Critical = 3,
}
