// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// The delay produced is exactly <c>min(BaseDelay * Multiplier^(attempt-1), MaxDelay)</c>.
/// </para>
/// <para>
/// <b>This type names the growth law and nothing else.</b> It previously carried a <c>UseJitter</c> flag and
/// a <c>JitterFactor</c>, which made randomisation selectable from a value type and gave this package a
/// second jitter implementation with a different distribution from the calculator's. Randomness is now
/// chosen by WHICH CALCULATOR YOU CONSTRUCT -- see <see cref="BackoffCalculatorFactory" /> -- so a
/// jitter band has no spelling on the public surface rather than merely being discouraged.
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
}
