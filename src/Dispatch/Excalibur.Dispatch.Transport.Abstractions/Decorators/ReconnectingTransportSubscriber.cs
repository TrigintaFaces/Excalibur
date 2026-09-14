// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportSubscriber"/> to make the receive/stream loop self-healing: when the
/// inner subscription faults with a non-cancellation error, this decorator backs off and re-subscribes,
/// so transient receive/stream faults no longer silently kill the subscriber. Cooperative cancellation
/// (an <see cref="OperationCanceledException"/> on the supplied token) propagates and is never retried.
/// </summary>
/// <remarks>
/// <para>
/// Different transports diverge on poll-loop failure (some abort the subscription on a receive-level error,
/// some silently stop on stream-end, none apply backoff). This shared decorator gives them a uniform
/// reconnect/backoff contract.
/// </para>
/// <para>
/// The backoff schedule is supplied as a <see cref="Func{T, TResult}"/> delegate (attempt number → delay)
/// rather than taking a resilience-library dependency — exactly mirroring how
/// <c>DeadLetterTransportSubscriber</c> takes a <c>deadLetterHandler</c> delegate to keep
/// <c>Transport.Abstractions</c> lightweight (no forced infrastructure dependency). The DI/transport layer
/// owns the concrete schedule (e.g. an in-house exponential calculator, or a consumer-supplied one).
/// </para>
/// </remarks>
internal sealed partial class ReconnectingTransportSubscriber : DelegatingTransportSubscriber
{
	/// <summary>
	/// The smallest wait this decorator will take between re-subscribes, whatever the schedule returns.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The reconnect loop is deliberately unbounded — a subscriber that gives up stops consuming with no
	/// error and never resumes — so the schedule is the only thing governing its pace. A schedule returning
	/// <see cref="TimeSpan.Zero"/> against a permanently-faulting inner therefore re-subscribes as fast as
	/// the machine allows, with nothing to stop it; a schedule returning a NEGATIVE value is worse, because
	/// <see cref="Task.Delay(TimeSpan, CancellationToken)"/> rejects it and the resulting exception is
	/// raised outside the catch that handles receive faults, killing the subscriber outright.
	/// </para>
	/// <para>
	/// One millisecond is chosen because it is the largest floor that cannot be said to override a caller's
	/// choice: the platform timer this delay runs on has a coarser resolution than that, so no schedule can
	/// express a wait the floor shortens. It buys nothing but the yield, which is the whole of the defect.
	/// The upper bound stays the caller's — a schedule's own maximum caps how long the wait grows.
	/// </para>
	/// </remarks>
	private static readonly TimeSpan MinimumReconnectDelay = TimeSpan.FromMilliseconds(1);

	private readonly Func<int, TimeSpan> _backoffDelay;
	private readonly ILogger<ReconnectingTransportSubscriber> _logger;

	// Reported once per subscriber, not once per attempt: the condition repeats on every reconnect, and a
	// warning per attempt against a fast schedule is its own denial of service on the log sink.
	private int _floorReported;

	/// <summary>
	/// Initializes a new instance of the <see cref="ReconnectingTransportSubscriber"/> class.
	/// </summary>
	/// <param name="innerSubscriber">The inner subscriber to decorate.</param>
	/// <param name="backoffDelay">
	/// The backoff schedule: given the 1-based reconnect attempt number, returns the delay to wait before the
	/// next re-subscribe. Required (no default) — the DI/transport layer supplies the concrete schedule.
	/// </param>
	/// <param name="logger">The logger for reconnect diagnostics.</param>
	public ReconnectingTransportSubscriber(
		ITransportSubscriber innerSubscriber,
		Func<int, TimeSpan> backoffDelay,
		ILogger<ReconnectingTransportSubscriber> logger) : base(innerSubscriber)
	{
		_backoffDelay = backoffDelay ?? throw new ArgumentNullException(nameof(backoffDelay));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public override async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		var attempt = 0;

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				await base.SubscribeAsync(handler, cancellationToken).ConfigureAwait(false);

				// The inner subscription ended on its own terms (e.g. the token was cancelled, or the
				// stream completed normally) — do NOT reconnect.
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Cooperative cancellation is not a fault — propagate, never retry.
				throw;
			}
			catch (Exception ex)
			{
				attempt++;
				var delay = ApplyFloor(_backoffDelay(attempt), attempt);
				LogReconnecting(ex, Source, attempt, delay.TotalMilliseconds);

				// Honors cancellation during the backoff wait: a cancel here throws OCE which propagates
				// out (it is not a receive fault, so it is not caught by the general handler above).
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Raises a schedule's delay to <see cref="MinimumReconnectDelay"/> when it falls below it, and says so
	/// the first time it happens on this subscriber.
	/// </summary>
	/// <remarks>
	/// Clamping rather than throwing is deliberate. Refusing the schedule would end the subscription, which
	/// is the failure this decorator exists to prevent — a subscriber that stops consuming and reports
	/// nothing. The clamp is announced instead, because a schedule that is being silently overridden is
	/// indistinguishable from one that is being honoured.
	/// </remarks>
	private TimeSpan ApplyFloor(TimeSpan scheduled, int attempt)
	{
		if (scheduled >= MinimumReconnectDelay)
		{
			return scheduled;
		}

		if (Interlocked.Exchange(ref _floorReported, 1) == 0)
		{
			LogBackoffFloorApplied(
				Source, attempt, scheduled.TotalMilliseconds, MinimumReconnectDelay.TotalMilliseconds);
		}

		return MinimumReconnectDelay;
	}

	[LoggerMessage(TransportAbstractionsEventId.SubscriberReconnecting, LogLevel.Warning,
		"Transport subscriber for source {Source} faulted on its receive/stream loop; reconnecting (attempt {Attempt}) after {DelayMs}ms backoff")]
	partial void LogReconnecting(Exception ex, string source, int attempt, double delayMs);

	[LoggerMessage(TransportAbstractionsEventId.SubscriberBackoffFloorApplied, LogLevel.Warning,
		"Reconnect backoff schedule for source {Source} returned {ScheduledMs}ms on attempt {Attempt}, below the "
		+ "{FloorMs}ms floor this subscriber enforces; the floor is being used instead. The reconnect loop is "
		+ "unbounded, so a delay at or below zero would re-subscribe without pause for as long as the inner "
		+ "subscription keeps faulting. Supply a schedule with a positive lower bound.")]
	partial void LogBackoffFloorApplied(string source, int attempt, double scheduledMs, double floorMs);
}
