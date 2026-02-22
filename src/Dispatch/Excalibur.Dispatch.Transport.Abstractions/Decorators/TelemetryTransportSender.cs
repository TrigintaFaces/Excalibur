// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportSender"/> with OpenTelemetry metrics and distributed tracing.
/// Records <c>dispatch.transport.messages.sent</c>, <c>dispatch.transport.messages.send_failed</c>,
/// <c>dispatch.transport.send.duration</c>, and <c>dispatch.transport.batch.size</c>.
/// </summary>
public sealed class TelemetryTransportSender : DelegatingTransportSender
{
	private readonly Counter<long> _sentCounter;
	private readonly Counter<long> _failedCounter;
	private readonly Histogram<double> _durationHistogram;
	private readonly Histogram<int> _batchSizeHistogram;
	private readonly ActivitySource _activitySource;
	private readonly string _transportName;
	private readonly TagCardinalityGuard _destinationGuard;
	private readonly TagCardinalityGuard _errorTypeGuard;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryTransportSender"/> class.
	/// </summary>
	/// <param name="innerSender">The inner sender to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <param name="transportName">The transport name for tagging (e.g., "Kafka", "AzureServiceBus").</param>
	public TelemetryTransportSender(
		ITransportSender innerSender,
		Meter meter,
		ActivitySource activitySource,
		string transportName) : base(innerSender)
	{
		ArgumentNullException.ThrowIfNull(meter);
		_activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
		_transportName = transportName ?? throw new ArgumentNullException(nameof(transportName));
		_destinationGuard = new TagCardinalityGuard(maxCardinality: 100);
		_errorTypeGuard = new TagCardinalityGuard(maxCardinality: 50);

		_sentCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesSent,
			"messages",
			"Total messages sent successfully");

		_failedCounter = meter.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesSendFailed,
			"messages",
			"Total message send failures");

		_durationHistogram = meter.CreateHistogram<double>(
			TransportTelemetryConstants.MetricNames.SendDuration,
			"ms",
			"Duration of send operations in milliseconds");

		_batchSizeHistogram = meter.CreateHistogram<int>(
			TransportTelemetryConstants.MetricNames.BatchSize,
			"messages",
			"Number of messages in a batch operation");
	}

	/// <inheritdoc />
	public override async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		var guardedDestination = _destinationGuard.Guard(Destination);

		using var activity = _activitySource.StartActivity("transport.send");
		activity?.SetTag(TransportTelemetryConstants.Tags.TransportName, _transportName);
		activity?.SetTag(TransportTelemetryConstants.Tags.Destination, guardedDestination);
		activity?.SetTag(TransportTelemetryConstants.Tags.Operation, "send");

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var result = await base.SendAsync(message, cancellationToken).ConfigureAwait(false);
			if (result.IsSuccess)
			{
				var tags = new TagList
				{
					{ TransportTelemetryConstants.Tags.TransportName, _transportName },
					{ TransportTelemetryConstants.Tags.Destination, guardedDestination },
				};
				_sentCounter.Add(1, tags);
			}
			else
			{
				var tags = new TagList
				{
					{ TransportTelemetryConstants.Tags.TransportName, _transportName },
					{ TransportTelemetryConstants.Tags.Destination, guardedDestination },
					{ TransportTelemetryConstants.Tags.ErrorType, _errorTypeGuard.Guard(result.Error?.Code ?? "unknown") },
				};
				_failedCounter.Add(1, tags);
				activity?.SetStatus(ActivityStatusCode.Error, result.Error?.Message);
			}

			var durationTags = new TagList
			{
				{ TransportTelemetryConstants.Tags.TransportName, _transportName },
				{ TransportTelemetryConstants.Tags.Destination, guardedDestination },
			};
			_durationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, durationTags);

			return result;
		}
		catch (Exception ex)
		{
			var failTags = new TagList
			{
				{ TransportTelemetryConstants.Tags.TransportName, _transportName },
				{ TransportTelemetryConstants.Tags.Destination, guardedDestination },
				{ TransportTelemetryConstants.Tags.ErrorType, _errorTypeGuard.Guard(ex.GetType().Name) },
			};
			_failedCounter.Add(1, failTags);

			var durationTags = new TagList
			{
				{ TransportTelemetryConstants.Tags.TransportName, _transportName },
				{ TransportTelemetryConstants.Tags.Destination, guardedDestination },
			};
			_durationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, durationTags);

			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}

	/// <inheritdoc />
	public override async Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		var guardedDestination = _destinationGuard.Guard(Destination);

		using var activity = _activitySource.StartActivity("transport.send_batch");
		activity?.SetTag(TransportTelemetryConstants.Tags.TransportName, _transportName);
		activity?.SetTag(TransportTelemetryConstants.Tags.Destination, guardedDestination);
		activity?.SetTag(TransportTelemetryConstants.Tags.Operation, "send_batch");

		var batchTags = new TagList
		{
			{ TransportTelemetryConstants.Tags.TransportName, _transportName },
			{ TransportTelemetryConstants.Tags.Destination, guardedDestination },
		};
		_batchSizeHistogram.Record(messages.Count, batchTags);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var result = await base.SendBatchAsync(messages, cancellationToken).ConfigureAwait(false);

			if (result.SuccessCount > 0)
			{
				_sentCounter.Add(result.SuccessCount, batchTags);
			}

			if (result.FailureCount > 0)
			{
				_failedCounter.Add(result.FailureCount, batchTags);
			}

			_durationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, batchTags);

			return result;
		}
		catch (Exception ex)
		{
			var failTags = new TagList
			{
				{ TransportTelemetryConstants.Tags.TransportName, _transportName },
				{ TransportTelemetryConstants.Tags.Destination, guardedDestination },
				{ TransportTelemetryConstants.Tags.ErrorType, _errorTypeGuard.Guard(ex.GetType().Name) },
			};
			_failedCounter.Add(messages.Count, failTags);

			_durationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, batchTags);

			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}
}
