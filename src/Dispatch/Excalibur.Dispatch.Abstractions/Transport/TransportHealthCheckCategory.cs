// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Categories of health checks for transport components.
/// </summary>
[Flags]
public enum TransportHealthCheckCategory
{
	/// <summary>
	/// No health check categories.
	/// </summary>
	None = 0,

	/// <summary>
	/// Connectivity health checks (network, connection pools).
	/// </summary>
	Connectivity = 1 << 0,

	/// <summary>
	/// Performance health checks (latency, throughput).
	/// </summary>
	Performance = 1 << 1,

	/// <summary>
	/// Resource health checks (memory, CPU, connections).
	/// </summary>
	Resources = 1 << 2,

	/// <summary>
	/// Configuration health checks (settings validation).
	/// </summary>
	Configuration = 1 << 3,

	/// <summary>
	/// All health check categories.
	/// </summary>
	All = Connectivity | Performance | Resources | Configuration,
}
