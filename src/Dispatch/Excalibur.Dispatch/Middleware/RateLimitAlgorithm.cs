// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Rate limiting algorithm types.
/// </summary>
public enum RateLimitAlgorithm
{
	/// <summary>
	/// Token bucket algorithm for smooth rate limiting.
	/// </summary>
	TokenBucket = 0,

	/// <summary>
	/// Sliding window algorithm for accurate rate limiting.
	/// </summary>
	SlidingWindow = 1,

	/// <summary>
	/// Fixed window algorithm for simple rate limiting.
	/// </summary>
	FixedWindow = 2,

	/// <summary>
	/// Concurrency limiter for limiting parallel executions.
	/// </summary>
	Concurrency = 3,
}
