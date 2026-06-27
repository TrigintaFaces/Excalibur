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
/// reconnect/backoff contract (bd-kxexrz).
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
	private readonly Func<int, TimeSpan> _backoffDelay;
	private readonly ILogger<ReconnectingTransportSubscriber> _logger;

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
				var delay = _backoffDelay(attempt);
				LogReconnecting(ex, Source, attempt, delay.TotalMilliseconds);

				// Honors cancellation during the backoff wait: a cancel here throws OCE which propagates
				// out (it is not a receive fault, so it is not caught by the general handler above).
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	[LoggerMessage(TransportAbstractionsEventId.SubscriberReconnecting, LogLevel.Warning,
		"Transport subscriber for source {Source} faulted on its receive/stream loop; reconnecting (attempt {Attempt}) after {DelayMs}ms backoff")]
	partial void LogReconnecting(Exception ex, string source, int attempt, double delayMs);
}
