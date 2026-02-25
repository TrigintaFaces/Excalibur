// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents the health status of a connection.
/// </summary>
public enum ConnectionHealth
{
	/// <summary>
	/// The connection is healthy and can be used.
	/// </summary>
	Healthy = 0,

	/// <summary>
	/// The connection is unhealthy and should be disposed.
	/// </summary>
	Unhealthy = 1,

	/// <summary>
	/// The connection health is unknown.
	/// </summary>
	Unknown = 2,
}
