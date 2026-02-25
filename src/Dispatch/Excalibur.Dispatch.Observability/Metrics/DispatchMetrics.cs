// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Provides centralized metrics collection for Dispatch messaging operations.
/// </summary>
public sealed class DispatchMetrics : IDispatchMetrics, IDisposable
{
	/// <summary>
	/// The meter name for Dispatch metrics.
	/// </summary>
	public const string MeterName = DispatchTelemetryConstants.Meters.Core;

	private readonly bool _ownsMeter;
	private readonly TagCardinalityGuard _messageTypeGuard = new();
	private readonly TagCardinalityGuard _handlerTypeGuard = new();
	private readonly TagCardinalityGuard _errorTypeGuard = new();
	private Counter<long> _messagesProcessed = null!;
	private Histogram<double> _processingDuration = null!;
	private Counter<long> _messagesPublished = null!;
	private Counter<long> _messagesFailed = null!;
	private UpDownCounter<int> _activeSessions = null!;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchMetrics" /> class.
	/// </summary>
	public DispatchMetrics()
	{
		Meter = new Meter(MeterName);
		_ownsMeter = true;
		InitializeInstruments();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchMetrics" /> class using an <see cref="IMeterFactory"/>.
	/// </summary>
	/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
	public DispatchMetrics(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);
		Meter = meterFactory.Create(MeterName);
		InitializeInstruments();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchMetrics" /> class using a shared telemetry provider.
	/// </summary>
	/// <param name="telemetryProvider"> The telemetry provider for shared meter management. </param>
	public DispatchMetrics(IDispatchTelemetryProvider telemetryProvider)
	{
		ArgumentNullException.ThrowIfNull(telemetryProvider);
		Meter = telemetryProvider.GetMeter(MeterName);
		InitializeInstruments();
	}

	/// <summary>
	/// Gets the meter instance.
	/// </summary>
	public Meter Meter { get; }

	/// <summary>
	/// Records a processed message metric.
	/// </summary>
	/// <param name="messageType"> The type of message processed. </param>
	/// <param name="handlerType"> The type of handler that processed the message. </param>
	/// <param name="tags"> Optional additional tags. </param>
	public void RecordMessageProcessed(string messageType, string handlerType, params (string Key, object? Value)[] tags)
	{
		ArgumentNullException.ThrowIfNull(tags);

		var tagList = new TagList
		{
			{ "message_type", _messageTypeGuard.Guard(messageType) }, { "handler_type", _handlerTypeGuard.Guard(handlerType) },
		};

		for (var i = 0; i < tags.Length; i++)
		{
			tagList.Add(tags[i].Key, tags[i].Value);
		}

		_messagesProcessed.Add(1, tagList);
	}

	/// <summary>
	/// Records message processing duration.
	/// </summary>
	/// <param name="duration"> The processing duration in milliseconds. </param>
	/// <param name="messageType"> The type of message processed. </param>
	/// <param name="success"> Whether the processing was successful. </param>
	public void RecordProcessingDuration(double duration, string messageType, bool success) =>
		_processingDuration.Record(duration,
			new TagList { { "message_type", _messageTypeGuard.Guard(messageType) }, { "success", success }, });

	/// <summary>
	/// Records a published message metric.
	/// </summary>
	/// <param name="messageType"> The type of message published. </param>
	/// <param name="destination"> The destination where the message was published. </param>
	public void RecordMessagePublished(string messageType, string destination) =>
		_messagesPublished.Add(1,
			new TagList { { "message_type", _messageTypeGuard.Guard(messageType) }, { "destination", destination }, });

	/// <summary>
	/// Records a failed message metric.
	/// </summary>
	/// <param name="messageType"> The type of message that failed. </param>
	/// <param name="errorType"> The type of error that occurred. </param>
	/// <param name="retryAttempt"> The retry attempt number. </param>
	public void RecordMessageFailed(string messageType, string errorType, int retryAttempt) =>
		_messagesFailed.Add(1,
			new TagList
			{
				{ "message_type", _messageTypeGuard.Guard(messageType) },
				{ "error_type", _errorTypeGuard.Guard(errorType) },
				{ "retry_attempt", retryAttempt },
			});

	/// <summary>
	/// Updates the active sessions counter.
	/// </summary>
	/// <param name="delta"> The change in active session count (positive for connect, negative for disconnect). </param>
	public void UpdateActiveSessions(int delta) => _activeSessions.Add(delta);

	/// <inheritdoc />
	public void Dispose()
	{
		// Only dispose the meter if this instance created it (not shared via IDispatchTelemetryProvider)
		if (_ownsMeter)
		{
			Meter.Dispose();
		}
	}

	private void InitializeInstruments()
	{
		_messagesProcessed = Meter.CreateCounter<long>("dispatch.messages.processed", "count", "Total number of messages processed");
		_processingDuration = Meter.CreateHistogram<double>("dispatch.messages.duration", "ms", "Message processing duration");
		_messagesPublished = Meter.CreateCounter<long>("dispatch.messages.published", "count", "Total number of messages published");
		_messagesFailed = Meter.CreateCounter<long>("dispatch.messages.failed", "count", "Total number of messages failed");
		_activeSessions = Meter.CreateUpDownCounter<int>("dispatch.sessions.active", "count", "Number of active sessions");
	}
}
