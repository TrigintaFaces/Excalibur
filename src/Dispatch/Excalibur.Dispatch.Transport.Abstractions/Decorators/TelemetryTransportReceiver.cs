// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportReceiver"/> with OpenTelemetry metrics and distributed tracing.
/// Records <c>dispatch.transport.messages.received</c>, <c>dispatch.transport.messages.acknowledged</c>,
/// <c>dispatch.transport.messages.rejected</c>, and <c>dispatch.transport.receive.duration</c>.
/// </summary>
public sealed class TelemetryTransportReceiver : DelegatingTransportReceiver
{
	private readonly Counter<long> _receivedCounter;
	private readonly Counter<long> _acknowledgedCounter;
	private readonly Counter<long> _rejectedCounter;
	private readonly Histogram<double> _durationHistogram;
	private readonly ActivitySource _activitySource;
	private readonly string _transportName;
	private readonly TagCardinalityGuard _sourceGuard;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryTransportReceiver"/> class.
	/// </summary>
	/// <param name="innerReceiver">The inner receiver to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <param name="transportName">The transport name for tagging (e.g., "Kafka", "AzureServiceBus").</param>
	public TelemetryTransportReceiver(
		ITransportReceiver innerReceiver,
		Meter meter,
		ActivitySource activitySource,
		string transportName) : base(innerReceiver)
	{
		ArgumentNullException.ThrowIfNull(meter);
		_activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
		_transportName = transportName ?? throw new ArgumentNullException(nameof(transportName));
		_sourceGuard = new TagCardinalityGuard(maxCardinality: 100);

		_receivedCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesReceived,
			"messages",
			"Total messages received");

		_acknowledgedCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesAcknowledged,
			"messages",
			"Total messages acknowledged");

		_rejectedCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesRejected,
			"messages",
			"Total messages rejected");

		_durationHistogram = meter.CreateHistogram<double>(
			TransportTelemetryConstants.MetricNames.ReceiveDuration,
			"ms",
			"Duration of receive operations in milliseconds");
	}

	/// <inheritdoc />
	public override async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		var guardedSource = _sourceGuard.Guard(Source);

		using var activity = _activitySource.StartActivity("transport.receive");
		activity?.SetTag(TransportTelemetryConstants.Tags.TransportName, _transportName);
		activity?.SetTag(TransportTelemetryConstants.Tags.Source, guardedSource);
		activity?.SetTag(TransportTelemetryConstants.Tags.Operation, "receive");

		var stopwatch = Stopwatch.StartNew();
		var messages = await base.ReceiveAsync(maxMessages, cancellationToken).ConfigureAwait(false);
		stopwatch.Stop();

		if (messages.Count > 0)
		{
			var tags = new TagList
			{
				{ TransportTelemetryConstants.Tags.TransportName, _transportName },
				{ TransportTelemetryConstants.Tags.Source, guardedSource },
			};
			_receivedCounter.Add(messages.Count, tags);
		}

		var durationTags = new TagList
		{
			{ TransportTelemetryConstants.Tags.TransportName, _transportName },
			{ TransportTelemetryConstants.Tags.Source, guardedSource },
		};
		_durationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, durationTags);

		return messages;
	}

	/// <inheritdoc />
	public override async Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		await base.AcknowledgeAsync(message, cancellationToken).ConfigureAwait(false);

		var tags = new TagList
		{
			{ TransportTelemetryConstants.Tags.TransportName, _transportName },
			{ TransportTelemetryConstants.Tags.Source, _sourceGuard.Guard(Source) },
		};
		_acknowledgedCounter.Add(1, tags);
	}

	/// <inheritdoc />
	public override async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		await base.RejectAsync(message, reason, requeue, cancellationToken).ConfigureAwait(false);

		var tags = new TagList
		{
			{ TransportTelemetryConstants.Tags.TransportName, _transportName },
			{ TransportTelemetryConstants.Tags.Source, _sourceGuard.Guard(Source) },
		};
		_rejectedCounter.Add(1, tags);
	}
}
