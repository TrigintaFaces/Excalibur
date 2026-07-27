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

	/// <summary>
	/// AWS "Full Jitter" exponential backoff: the delay is sampled uniformly from
	/// <c>[0, min(maxDelay, baseDelay * multiplier^(attempt-1))]</c>, maximally decorrelating concurrent
	/// clients to avoid the thundering-herd problem.
	/// </summary>
	FullJitter = 5,

	/// <summary>
	/// AWS "Decorrelated Jitter": the delay is sampled uniformly from
	/// <c>[baseDelay, min(maxDelay, previousDelay * 3)]</c>, threading the previous actual delay forward for a
	/// smoother, less-correlated growth than full jitter. Stateful, so it applies to the in-process retry path
	/// only; durable retry paths use an attempt-derived strategy.
	/// </summary>
	DecorrelatedJitter = 6,
}
