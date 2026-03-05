// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="TelemetryTransportSender"/>.
/// Verifies that OpenTelemetry metrics are recorded on send operations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TelemetryTransportSenderShould : IDisposable
{
	private readonly ITransportSender _innerSender = A.Fake<ITransportSender>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.Test", "1.0.0");
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Transport.Test");
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, long Value)> _recordedCounters = [];
	private readonly List<(string Name, double Value)> _recordedHistograms = [];
	private bool _disposed;

	public TelemetryTransportSenderShould()
	{
		A.CallTo(() => _innerSender.Destination).Returns("test-topic");

		_meterListener = new MeterListener();
		_meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			_recordedCounters.Add((instrument.Name, measurement));
		});

		_meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
		{
			_recordedHistograms.Add((instrument.Name, measurement));
		});

		_meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
		{
			_recordedHistograms.Add((instrument.Name, measurement));
		});

		_meterListener.Start();
	}

	[Fact]
	public async Task Record_SentCounter_On_Successful_Send()
	{
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		await sut.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesSent && c.Value == 1);
	}

	[Fact]
	public async Task Record_FailedCounter_On_Failed_Send()
	{
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Failure(new SendError { Code = "Timeout", Message = "Timed out" }));

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		await sut.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesSendFailed && c.Value == 1);
	}

	[Fact]
	public async Task Record_FailedCounter_On_Exception()
	{
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("boom"));

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None));

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesSendFailed && c.Value == 1);
	}

	[Fact]
	public async Task Record_DurationHistogram_On_Send()
	{
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		await sut.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);

		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.SendDuration && h.Value >= 0);
	}

	[Fact]
	public async Task Record_BatchSize_Histogram_On_BatchSend()
	{
		A.CallTo(() => _innerSender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Returns(new BatchSendResult { TotalMessages = 3, SuccessCount = 3 });

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		var messages = new[]
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
			TransportMessage.FromString("c"),
		};
		await sut.SendBatchAsync(messages, CancellationToken.None);

		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.BatchSize && (int)h.Value == 3);
	}

	[Fact]
	public async Task Record_SentCounter_With_BatchSuccessCount()
	{
		A.CallTo(() => _innerSender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Returns(new BatchSendResult { TotalMessages = 5, SuccessCount = 3, FailureCount = 2 });

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		var messages = Enumerable.Range(0, 5).Select(_ => TransportMessage.FromString("x")).ToArray();
		await sut.SendBatchAsync(messages, CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesSent && c.Value == 3);
		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesSendFailed && c.Value == 2);
	}

	[Fact]
	public async Task Record_DurationHistogram_On_BatchSend_Exception()
	{
		A.CallTo(() => _innerSender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Throws(new TimeoutException("batch timeout"));

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");

		await Should.ThrowAsync<TimeoutException>(
			() => sut.SendBatchAsync(
				[TransportMessage.FromString("a"), TransportMessage.FromString("b")],
				CancellationToken.None));

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesSendFailed && c.Value == 2);
		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.SendDuration && h.Value >= 0);
	}

	[Fact]
	public async Task Record_BatchSize_FailedCount_And_Duration_On_BatchSend_Exception()
	{
		A.CallTo(() => _innerSender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Throws(new TimeoutException("batch timeout"));

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		var messages = new[]
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
			TransportMessage.FromString("c"),
		};

		await Should.ThrowAsync<TimeoutException>(() => sut.SendBatchAsync(messages, CancellationToken.None));

		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.BatchSize && (int)h.Value == 3);
		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesSendFailed && c.Value == 3);
		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.SendDuration && h.Value >= 0);
	}

	[Fact]
	public async Task Record_DurationHistogram_On_Failed_Send_Result()
	{
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Failure(new SendError { Code = "validation", Message = "invalid payload" }));

		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		_ = await sut.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);

		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.SendDuration && h.Value >= 0);
	}

	[Fact]
	public void Throw_When_Meter_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new TelemetryTransportSender(_innerSender, null!, _activitySource, "Test"));
	}

	[Fact]
	public void Throw_When_ActivitySource_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new TelemetryTransportSender(_innerSender, _meter, null!, "Test"));
	}

	[Fact]
	public void Throw_When_TransportName_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new TelemetryTransportSender(_innerSender, _meter, _activitySource, null!));
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		(_innerSender as IDisposable)?.Dispose();
		_meterListener.Dispose();
		_meter.Dispose();
		_activitySource.Dispose();
	}
}
