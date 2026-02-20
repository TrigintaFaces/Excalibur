// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Pool health status.
/// </summary>
public enum PoolHealth
{
	/// <summary>
	/// Pool is operating normally.
	/// </summary>
	Healthy = 0,

	/// <summary>
	/// Pool is experiencing high utilization or other warning conditions.
	/// </summary>
	Warning = 1,

	/// <summary>
	/// Pool is in a critical state requiring attention.
	/// </summary>
	Critical = 2,
}
