// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0



using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Options.Scheduling;

/// <summary>
/// Options controlling the <see cref="ScheduledMessageService" /> polling behaviour.
/// </summary>
public sealed class SchedulerOptions
{
	/// <summary>
	/// Gets or sets how often the scheduler checks for messages to dispatch.
	/// </summary>
	/// <value>
	/// How often the scheduler checks for messages to dispatch.
	/// </value>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether adaptive polling backoff is enabled when the scheduler is idle.
	/// </summary>
	public bool EnableAdaptivePolling { get; set; }

	/// <summary>
	/// Gets or sets the minimum polling interval used when adaptive polling is enabled and work is present.
	/// </summary>
	public TimeSpan MinPollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the multiplier used to increase polling delay when the scheduler is idle.
	/// </summary>
	public double AdaptivePollingBackoffMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets the jitter ratio applied to polling delay to reduce synchronized wake-ups.
	/// </summary>
	/// <remarks>
	/// A value of 0 disables jitter. A value of 0.1 applies up to +/-10% random jitter.
	/// </remarks>
	public double PollingJitterRatio { get; set; }

	/// <summary>
	/// Gets or sets determines how messages scheduled in the past are handled.
	/// </summary>
	/// <value>The current <see cref="PastScheduleBehavior"/> value.</value>
	public PastScheduleBehavior PastScheduleBehavior { get; set; } = PastScheduleBehavior.ExecuteImmediately;

	/// <summary>
	/// Gets or sets how many times one occurrence may be admitted without its settlement being committed
	/// before the schedule is retired instead of dispatched again.
	/// </summary>
	/// <value>The admission bound per occurrence. Defaults to 5. Must be at least 1.</value>
	/// <remarks>
	/// <para>
	/// An occurrence whose settlement write is refused — because the host paused, migrated or was collected
	/// past its execution lease — is admitted again on the next poll, by design: a lapsed lease must produce
	/// a successor rather than a stop. Nothing in that loop retires the occurrence, so without a bound the
	/// same handler is invoked once per poll interval indefinitely while the schedule never advances.
	/// </para>
	/// <para>
	/// On the admission that exceeds this bound the handler is NOT invoked. The schedule is disabled and the
	/// occurrence settled under the still-live lease, which stops the loop and leaves an operator a durable
	/// row to inspect. Raise it for handlers that legitimately outrun their lease; prefer renewing the lease.
	/// </para>
	/// </remarks>
	public int MaxAdmissionAttemptsPerOccurrence { get; set; } = 5;
}
