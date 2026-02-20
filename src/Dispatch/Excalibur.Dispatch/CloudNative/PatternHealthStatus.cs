// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Health status for cloud-native patterns.
/// </summary>
public enum PatternHealthStatus
{
	/// <summary>
	/// Health status is unknown or not yet determined.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Pattern is operating normally with optimal performance.
	/// </summary>
	Healthy = 1,

	/// <summary>
	/// Pattern is functioning but with reduced performance or capacity.
	/// </summary>
	Degraded = 2,

	/// <summary>
	/// Pattern is not functioning properly and may be failing operations.
	/// </summary>
	Unhealthy = 3,

	/// <summary>
	/// Pattern is in a critical state and immediate attention is required.
	/// </summary>
	Critical = 4,
}
