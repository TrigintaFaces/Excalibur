// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Health;

/// <summary>
/// Health status enumeration for compatibility with enum-based systems.
/// </summary>
public enum HealthStatus
{
	/// <summary>
	/// The resource is healthy.
	/// </summary>
	Healthy = 0,

	/// <summary>
	/// The resource is unhealthy.
	/// </summary>
	Unhealthy = 1,

	/// <summary>
	/// The resource status is unknown or indeterminate.
	/// </summary>
	Unknown = 2,
}
