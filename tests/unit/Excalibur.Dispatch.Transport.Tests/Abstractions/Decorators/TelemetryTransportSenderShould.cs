// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
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
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class TelemetryTransportSenderShould : IDisposable
{
	private readonly ITransportSender _innerSender = A.Fake<ITransportSender>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.Test", "1.0.0");
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Transport.Test");
	private readonly MeterListener _meterListener;
	// ConcurrentBag, not List: a MeterListener callback runs on WHATEVER THREAD RECORDS THE MEASUREMENT.
	// List<T>.Add resizes with a Count-then-Array.Copy that a concurrent append invalidates, surfacing as
	// "Destination array was not long enough". Because InstrumentPublished filters by meter NAME, which every
	// instance of a decorator shares, the corrupting thread is often a DIFFERENT test class in the same
	// assembly — so the failure gets reported against that sibling and reproduces only in a full shard run.
	private readonly ConcurrentBag<(string Name, long Value)> _recordedCounters = [];
	private readonly ConcurrentBag<(string Name, double Value)> _recordedHistograms = [];
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
	[Fact]
	public async Task Not_Record_Any_Duration_Histogram()
	{
		// Operation duration is recorded once, by the transport adapter beneath this decorator.
		// Recording it here too produced two instruments for one quantity, which a dashboard
		// aggregating either one would double-count. This asserts the duplicate stays gone.
		var sut = new TelemetryTransportSender(_innerSender, _meter, _activitySource, "Test");
		_ = await sut.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);

		_recordedHistograms.ShouldNotContain(h => h.Name.Contains("duration", StringComparison.Ordinal));
	}
}
