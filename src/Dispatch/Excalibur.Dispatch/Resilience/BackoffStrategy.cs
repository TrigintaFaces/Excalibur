// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Defines the backoff strategies for retry delays.
/// </summary>
/// <remarks>
/// Canonical location for all backoff strategy definitions.
/// Previously duplicated in <c>Excalibur.Dispatch.Middleware</c>
/// and <c>Excalibur.Dispatch.Resilience.Polly</c>.
/// </remarks>
public enum BackoffStrategy
{
	/// <summary>
	/// Fixed (constant) delay between retry attempts.
	/// </summary>
	Fixed = 0,

	/// <summary>
	/// Linear increase in delay (baseDelay * attempt).
	/// </summary>
	Linear = 1,

	/// <summary>
	/// Exponential increase in delay (baseDelay * 2^attempt).
	/// </summary>
	Exponential = 2,

	/// <summary>
	/// Exponential increase with random jitter to prevent thundering herd.
	/// </summary>
	ExponentialWithJitter = 3,

	/// <summary>
	/// Fibonacci sequence delays.
	/// </summary>
	Fibonacci = 4,
}
