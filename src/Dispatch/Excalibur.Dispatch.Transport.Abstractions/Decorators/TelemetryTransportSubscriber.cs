// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportSubscriber"/> with OpenTelemetry metrics and distributed tracing.
/// Wraps both subscription lifecycle (<c>transport.subscribe</c> span) and per-message handler
/// invocations (<c>transport.process</c> span + metrics).
/// </summary>
/// <remarks>
/// <para>
/// Reuses existing receiver metric names for semantically identical operations:
/// <c>dispatch.transport.messages.received</c>, <c>dispatch.transport.messages.acknowledged</c>,
/// <c>dispatch.transport.messages.rejected</c>.
/// </para>
/// <para>
/// Adds subscriber-specific metrics: <c>dispatch.transport.messages.requeued</c>,
/// <c>dispatch.transport.handler.errors</c>, <c>dispatch.transport.handler.duration</c>.
/// </para>
/// </remarks>
public sealed class TelemetryTransportSubscriber : DelegatingTransportSubscriber
{
	private readonly Counter<long> _receivedCounter;
	private readonly Counter<long> _acknowledgedCounter;
	private readonly Counter<long> _rejectedCounter;
	private readonly Counter<long> _requeuedCounter;
	private readonly Counter<long> _handlerErrorCounter;
	private readonly Histogram<double> _handlerDurationHistogram;
	private readonly ActivitySource _activitySource;
	private readonly string _transportName;
	private readonly TagCardinalityGuard _sourceGuard;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryTransportSubscriber"/> class.
	/// </summary>
	/// <param name="innerSubscriber">The inner subscriber to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <param name="transportName">The transport name for tagging (e.g., "Kafka", "AzureServiceBus").</param>
	public TelemetryTransportSubscriber(
		ITransportSubscriber innerSubscriber,
		Meter meter,
		ActivitySource activitySource,
		string transportName) : base(innerSubscriber)
	{
		ArgumentNullException.ThrowIfNull(meter);
		ArgumentNullException.ThrowIfNull(activitySource);
		ArgumentException.ThrowIfNullOrEmpty(transportName);

		_activitySource = activitySource;
		_transportName = transportName;
		_sourceGuard = new TagCardinalityGuard(maxCardinality: 100);

		_receivedCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesReceived,
			"messages",
			"Total messages received by subscriber handler");

		_acknowledgedCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesAcknowledged,
			"messages",
			"Total messages acknowledged by subscriber handler");

		_rejectedCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesRejected,
			"messages",
			"Total messages rejected by subscriber handler");

		_requeuedCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesRequeued,
			"messages",
			"Total messages requeued by subscriber handler");

		_handlerErrorCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.HandlerErrors,
			"errors",
			"Total handler errors during subscriber message processing");

		_handlerDurationHistogram = meter.CreateHistogram<double>(
			TransportTelemetryConstants.MetricNames.HandlerDuration,
			"ms",
			"Duration of subscriber handler invocations in milliseconds");
	}

	/// <inheritdoc />
	public override async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		var guardedSource = _sourceGuard.Guard(Source);

		async Task<MessageAction> InstrumentedHandler(TransportReceivedMessage message, CancellationToken ct)
		{
			using var activity = _activitySource.StartActivity("transport.process");
			activity?.SetTag(TransportTelemetryConstants.Tags.TransportName, _transportName);
			activity?.SetTag(TransportTelemetryConstants.Tags.Source, guardedSource);
			activity?.SetTag(TransportTelemetryConstants.Tags.Operation, "process");

			var tags = new TagList
			{
				{ TransportTelemetryConstants.Tags.TransportName, _transportName },
				{ TransportTelemetryConstants.Tags.Source, guardedSource },
			};

			_receivedCounter.Add(1, tags);
			var stopwatch = Stopwatch.StartNew();

			try
			{
				var action = await handler(message, ct).ConfigureAwait(false);
				stopwatch.Stop();
				_handlerDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, tags);

				switch (action)
				{
					case MessageAction.Acknowledge:
						_acknowledgedCounter.Add(1, tags);
						break;
					case MessageAction.Reject:
						_rejectedCounter.Add(1, tags);
						break;
					case MessageAction.Requeue:
						_requeuedCounter.Add(1, tags);
						break;
				}

				return action;
			}
			catch (Exception ex)
			{
				stopwatch.Stop();
				_handlerErrorCounter.Add(1, tags);
				_handlerDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				throw;
			}
		}

		using var subscribeActivity = _activitySource.StartActivity("transport.subscribe");
		subscribeActivity?.SetTag(TransportTelemetryConstants.Tags.TransportName, _transportName);
		subscribeActivity?.SetTag(TransportTelemetryConstants.Tags.Source, guardedSource);

		await base.SubscribeAsync(InstrumentedHandler, cancellationToken).ConfigureAwait(false);
	}
}
