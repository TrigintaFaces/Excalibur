// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Defines the growth law for retry delays.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three growth laws, and jitter is a separate switch.</b> These members name only how the nominal
/// delay GROWS with the attempt number. Whether that delay is then randomised is
/// <c>RetryOptions.UseJitter</c>, which applies to every member here. Earlier versions folded jitter into
/// the strategy name, and that produced two public knobs for one concept whose interaction nothing
/// documented: a strategy called <c>ExponentialWithJitter</c> that ignored <c>UseJitter</c>, and a
/// <c>UseJitter</c> that the default strategy silently discarded.
/// </para>
/// <para>
/// <b>These are the same three laws, with the same names, as Polly's <c>DelayBackoffType</c>.</b> That is
/// deliberate: the opt-in <c>Excalibur.Dispatch.Resilience.Polly</c> package maps this enum onto Polly
/// one-to-one, exhaustively, with no fall-through. A consumer who knows Polly already knows this surface,
/// and -- more importantly -- a member cannot be added here and silently reinterpreted as something else
/// there, because the mapping has no default arm to absorb it. A new member is a compile error in the
/// adapter until it is mapped, which is where that decision belongs.
/// </para>
/// <para>
/// <b>If you need a curve that is not one of these</b> -- Fibonacci growth, a bespoke ladder, a sequence
/// read from configuration -- implement <see cref="IBackoffCalculator" /> and register it. That is the
/// escape hatch, and it is the same shape as Polly's own <c>DelayGenerator</c>: three laws cover the cases
/// worth naming, and anything else is a function the consumer supplies rather than a member we enumerate.
/// </para>
/// </remarks>
public enum BackoffStrategy
{
	/// <summary>
	/// The same delay before every attempt: <c>baseDelay</c>.
	/// </summary>
	Constant = 0,

	/// <summary>
	/// Delay grows in proportion to the attempt number: <c>min(baseDelay * attempt, maxDelay)</c>.
	/// </summary>
	Linear = 1,

	/// <summary>
	/// Delay grows geometrically: <c>min(baseDelay * multiplier^(attempt-1), maxDelay)</c>.
	/// </summary>
	Exponential = 2,
}
