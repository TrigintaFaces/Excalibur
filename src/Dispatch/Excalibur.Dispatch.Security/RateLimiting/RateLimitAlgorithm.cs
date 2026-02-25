// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Defines rate limiting algorithms.
/// </summary>
public enum RateLimitAlgorithm
{
	/// <summary>
	/// Unknown or unsupported algorithm (for forward compatibility).
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Token bucket algorithm - allows bursts up to bucket size.
	/// </summary>
	TokenBucket = 1,

	/// <summary>
	/// Sliding window algorithm - smooth rate limiting over time window.
	/// </summary>
	SlidingWindow = 2,

	/// <summary>
	/// Fixed window algorithm - resets at fixed intervals.
	/// </summary>
	FixedWindow = 3,

	/// <summary>
	/// Concurrency limiter - limits concurrent operations.
	/// </summary>
	Concurrency = 4,
}
