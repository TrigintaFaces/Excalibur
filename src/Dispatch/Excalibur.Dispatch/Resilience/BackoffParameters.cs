// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Immutable parameter set describing an exponential backoff schedule.
/// </summary>
/// <remarks>
/// <para>
/// This value carries the inputs consumed by <see cref="ExponentialBackoff.Calculate(int, in BackoffParameters)" />.
/// It is a lightweight, allocation-free <see langword="readonly" /> record struct intended to be constructed at a
/// call site (typically mapped from transport-specific retry options) and passed by <see langword="in" /> reference
/// to the stateless calculator.
/// </para>
/// <para>
/// The delay produced is <c>min(BaseDelay * Multiplier^(attempt-1), MaxDelay)</c>, with optional symmetric jitter
/// applied when <see cref="UseJitter" /> is <see langword="true" /> and <see cref="JitterFactor" /> is greater than zero.
/// </para>
/// </remarks>
public readonly record struct BackoffParameters
{
	/// <summary>
	/// Gets the base delay used for the first retry attempt.
	/// </summary>
	/// <value>The delay applied before the exponential multiplier is raised to a power.</value>
	public TimeSpan BaseDelay { get; init; }

	/// <summary>
	/// Gets the maximum delay cap. Computed delays are never larger than this value.
	/// </summary>
	/// <value>The upper bound on the produced delay.</value>
	public TimeSpan MaxDelay { get; init; }

	/// <summary>
	/// Gets the multiplier governing exponential growth between attempts (typically <c>2.0</c>).
	/// </summary>
	/// <value>The exponential base; values below <c>1.0</c> are clamped up to <c>1.0</c> by the calculator.</value>
	public double Multiplier { get; init; }

	/// <summary>
	/// Gets a value indicating whether symmetric random jitter is applied to the computed delay.
	/// </summary>
	/// <value><see langword="true" /> to randomize delays to avoid thundering-herd synchronization; otherwise, <see langword="false" />.</value>
	public bool UseJitter { get; init; }

	/// <summary>
	/// Gets the jitter factor (proportion of the delay) used when <see cref="UseJitter" /> is enabled.
	/// </summary>
	/// <value>A non-negative fraction; the delay is randomized within <c>±(delay × JitterFactor)</c>.</value>
	public double JitterFactor { get; init; }
}
