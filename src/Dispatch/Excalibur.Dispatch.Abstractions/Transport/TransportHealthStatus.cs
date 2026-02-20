// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Represents the health status of a transport component.
/// </summary>
public enum TransportHealthStatus
{
	/// <summary>
	/// The component is healthy and operating normally.
	/// </summary>
	Healthy = 0,

	/// <summary>
	/// The component is degraded but still functional.
	/// </summary>
	Degraded = 1,

	/// <summary>
	/// The component is unhealthy and not functioning properly.
	/// </summary>
	Unhealthy = 2,
}
