// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Cdc;

/// <summary>
/// Options for configuring fatal-error handling during CDC processing for a provider whose change
/// events are of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The provider-specific change-event type.</typeparam>
public sealed class CdcFatalErrorOptions<TEvent>
	where TEvent : class
{
	/// <summary>
	/// Gets or sets the delegate invoked when a fatal error occurs during CDC processing.
	/// When <see langword="null"/>, the processor rethrows the exception and stops processing
	/// (fail-loud — never a silent infinite retry).
	/// </summary>
	public CdcFatalErrorHandler<TEvent>? OnFatalError { get; set; }

	/// <summary>
	/// Gets or sets how many consecutive transient failures a streaming processor tolerates before it stops,
	/// or <see langword="null"/> to keep reconnecting for as long as the process runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A failure the framework does not recognise is treated as transient, so a condition that never clears
	/// would otherwise be retried indefinitely. The retry always backs off (see <see cref="MaxReconnectDelay"/>)
	/// and the count is reported to the CDC health check whether or not a limit is set; this limit adds a stop.
	/// </para>
	/// <para>
	/// On reaching the limit the processor stops through the same path as a fatal error: it invokes
	/// <see cref="OnFatalError"/> when one is set, and otherwise throws a
	/// <see cref="CdcRetryExhaustedException"/> carrying the last failure. The durable position is never
	/// advanced on the way out, so a restarted processor resumes where this one stopped.
	/// </para>
	/// <para>
	/// A failure only counts if its attempt made no progress and stayed up for less than
	/// <see cref="MaxReconnectDelay"/>, so a quiet source disconnected by an idle timeout is not stopped.
	/// </para>
	/// <para>
	/// <b>Size the limit against your shutdown grace period.</b> With a one-second base interval, five
	/// consecutive failures elapse in about thirty-one seconds, which is close to a typical termination grace
	/// period. During a rolling deployment the previous instance may still hold a resource (a replication
	/// slot, a lease) for that long, so a small limit can stop the new instance before the old one has
	/// released it.
	/// </para>
	/// </remarks>
	/// <value>The limit, at least one; or <see langword="null"/> (the default) for no limit.</value>
	public int? MaxConsecutiveTransientFailures { get; set; }

	/// <summary>
	/// Gets or sets the longest delay between reconnect attempts after a transient failure.
	/// </summary>
	/// <remarks>
	/// The delay starts at the provider's own polling or reconnect interval and doubles with each
	/// consecutive failure until it reaches this value. A connection that stays up at least this long before
	/// failing is treated as healthy, and its failure starts a new count. A value below the provider's own
	/// interval is raised to that interval.
	/// </remarks>
	/// <value>The maximum delay; defaults to one minute. Must be greater than zero.</value>
	public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromMinutes(1);
}
