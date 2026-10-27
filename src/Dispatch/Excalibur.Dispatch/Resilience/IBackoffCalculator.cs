// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Computes how long to wait before a retry attempt.
/// </summary>
/// <remarks>
/// <para>
/// <b>This interface states its contract because two implementations once disagreed about it.</b> Some
/// calculators threw for a non-positive attempt while another returned
/// <see cref="TimeSpan.Zero" />; one refused a zero base delay that the others
/// accepted. Both were defensible against a specification that said nothing, and a consumer swapping one
/// for the other -- which is the advertised purpose of this seam -- got a different answer to the same
/// call. The contract below is what makes them substitutable. An implementation that does not satisfy it
/// is not a valid implementation of this interface, however reasonable it looks on its own.
/// </para>
/// </remarks>
public interface IBackoffCalculator
{
	/// <summary>
	/// Calculates the delay to wait before the attempt numbered <paramref name="attempt" />.
	/// </summary>
	/// <param name="attempt">The 1-based attempt number. The first retry is attempt 1.</param>
	/// <returns>
	/// A non-negative delay, never greater than the maximum this calculator was configured with.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Requires:</b> <paramref name="attempt" /> is 1 or greater. An implementation MUST throw
	/// <see cref="ArgumentOutOfRangeException" /> for 0 or a negative value, and MUST NOT return a value.
	/// A zero delay and an invalid argument are different facts, and an implementation that returns
	/// <see cref="TimeSpan.Zero" /> instead of throwing converts a caller's off-by-one into an immediate
	/// retry with no backoff -- the exact behaviour backoff exists to prevent, delivered silently.
	/// </para>
	/// <para>
	/// <b>Guarantees:</b> the returned delay is greater than or equal to <see cref="TimeSpan.Zero" /> and
	/// less than or equal to the configured maximum. Overflow at a high attempt number MUST clamp to that
	/// maximum; it MUST NOT wrap, and MUST NOT return a negative delay, which callers pass straight to
	/// <see cref="System.Threading.Tasks.Task.Delay(TimeSpan, System.Threading.CancellationToken)" /> and
	/// which throws there instead -- on the failure path, which is the worst place to surface it.
	/// </para>
	/// <para>
	/// <b>Jitter has a FLOOR and a CEILING, and the nominal delay is not part of either.</b> When an
	/// implementation randomises, then over repeated calls at a fixed <paramref name="attempt" /> the
	/// delay MUST take a value below half the configured maximum with positive probability, and MUST be
	/// independent of every other caller's delay. The DISTRIBUTION between those bounds is deliberately
	/// implementation-defined.
	/// </para>
	/// <para>
	/// <b>The floor is the clause that carries the meaning, and the earlier contract omitted it.</b> This
	/// paragraph used to require the delay to lie within <c>[0, nominal]</c> -- the nominal being what the
	/// strategy prescribes for the attempt. That was wrong twice over. It is UNOBSERVABLE: a caller holds
	/// this interface and has no operation that yields the nominal, so it could not check the one thing it
	/// was told to rely on. And a ceiling alone is satisfied completely by returning the nominal unchanged,
	/// which is to say by applying no jitter at all -- so the old clause permitted an implementation that
	/// decorrelates nothing while appearing conformant. Sampling down toward zero is the entire purpose:
	/// two callers that failed together must not retry together.
	/// </para>
	/// <para>
	/// <b>A symmetric band around the nominal is therefore NOT a valid implementation of this interface</b>,
	/// which is deliberate. A band adds noise without decorrelating -- its mass sits near the nominal, so
	/// clients stay clustered -- and when the nominal ladder grows past the configured maximum, a band that
	/// is capped AFTER randomisation collapses to exactly the maximum with probability one, taking the
	/// jitter to zero precisely during the sustained outage that jitter exists for. An implementation MUST
	/// cap FIRST and randomise below the capped value.
	/// </para>
	/// <para>
	/// <b>Statelessness.</b> The delay MUST be a function of <paramref name="attempt" /> and the
	/// configuration alone, not of previously returned values. A calculator whose output depends on its
	/// own history cannot be shared across messages -- one message's first retry would be computed from
	/// another's last delay -- and cannot survive the process restart that a durable retry path assumes.
	/// Implementations are consequently safe to register as singletons and to call concurrently.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="attempt" /> is less than 1.</exception>
	TimeSpan CalculateDelay(int attempt);
}
