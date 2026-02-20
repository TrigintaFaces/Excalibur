// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// States for resilience patterns.
/// </summary>
public enum ResilienceState
{
	/// <summary>
	/// Normal operation - requests are allowed through and failures are monitored.
	/// </summary>
	Closed = 0,

	/// <summary>
	/// Failing fast - requests are rejected immediately without attempting execution.
	/// </summary>
	Open = 1,

	/// <summary>
	/// Testing recovery - a limited number of requests are allowed to test if the service has recovered.
	/// </summary>
	HalfOpen = 2,
}
