// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Defines schema compatibility modes for CloudEvents.
/// </summary>
public enum SchemaCompatibilityMode
{
	/// <summary>
	/// No compatibility guarantees.
	/// </summary>
	None = 0,

	/// <summary>
	/// Forward compatibility - newer versions can read older data.
	/// </summary>
	Forward = 1,

	/// <summary>
	/// Backward compatibility - older versions can read newer data.
	/// </summary>
	Backward = 2,

	/// <summary>
	/// Full compatibility - both forward and backward compatible.
	/// </summary>
	Full = 3,
}
