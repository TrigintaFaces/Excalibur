// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="TelemetryTransportReceiver"/>.
/// Verifies that OpenTelemetry metrics are recorded on receive, acknowledge, and reject operations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TelemetryTransportReceiverShould : IDisposable
{
	private readonly ITransportReceiver _innerReceiver = A.Fake<ITransportReceiver>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.ReceiverTest", "1.0.0");
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Transport.ReceiverTest");
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, long Value)> _recordedCounters = [];
	private readonly List<(string Name, double Value)> _recordedHistograms = [];
	private bool _disposed;

	public TelemetryTransportReceiverShould()
	{
		A.CallTo(() => _innerReceiver.Source).Returns("test-queue");

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
	public async Task Record_ReceivedCounter_When_Messages_Returned()
	{
		var messages = new List<TransportReceivedMessage> { CreateTestMessage(), CreateTestMessage() };
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(messages);

		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");
		await sut.ReceiveAsync(10, CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesReceived && c.Value == 2);
	}

	[Fact]
	public async Task Not_Record_ReceivedCounter_When_No_Messages()
	{
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage>());

		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");
		await sut.ReceiveAsync(10, CancellationToken.None);

		_recordedCounters.ShouldNotContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesReceived);
	}

	[Fact]
	public async Task Record_AcknowledgedCounter_On_AcknowledgeAsync()
	{
		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");
		await sut.AcknowledgeAsync(CreateTestMessage(), CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesAcknowledged && c.Value == 1);
	}

	[Fact]
	public async Task Record_RejectedCounter_On_RejectAsync()
	{
		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");
		await sut.RejectAsync(CreateTestMessage(), "bad message", false, CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesRejected && c.Value == 1);
	}

	[Fact]
	public async Task Record_DurationHistogram_On_ReceiveAsync()
	{
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage>());

		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");
		await sut.ReceiveAsync(10, CancellationToken.None);

		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.ReceiveDuration && h.Value >= 0);
	}

	[Fact]
	public async Task Record_DurationHistogram_On_ReceiveAsync_WithReturnedMessages()
	{
		var messages = new List<TransportReceivedMessage> { CreateTestMessage() };
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(messages);

		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");
		_ = await sut.ReceiveAsync(10, CancellationToken.None);

		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.ReceiveDuration && h.Value >= 0);
	}

	[Fact]
	public async Task Propagate_Exception_From_InnerReceive_Without_Recording_Success_Metrics()
	{
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("receive failed"));

		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");

		await Should.ThrowAsync<InvalidOperationException>(() => sut.ReceiveAsync(10, CancellationToken.None));

		_recordedCounters.ShouldNotContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesReceived);
		_recordedHistograms.ShouldNotContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.ReceiveDuration);
	}

	[Fact]
	public async Task Create_Activity_With_Correct_Tags()
	{
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage>());

		Activity? capturedActivity = null;
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Excalibur.Dispatch.Transport.ReceiverTest",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = activity => capturedActivity = activity,
		};
		ActivitySource.AddActivityListener(listener);

		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");
		await sut.ReceiveAsync(10, CancellationToken.None);

		capturedActivity.ShouldNotBeNull();
		capturedActivity.GetTagItem(TransportTelemetryConstants.Tags.TransportName).ShouldBe("Test");
		capturedActivity.GetTagItem(TransportTelemetryConstants.Tags.Source).ShouldBe("test-queue");
		capturedActivity.GetTagItem(TransportTelemetryConstants.Tags.Operation).ShouldBe("receive");
	}

	[Fact]
	public async Task Delegate_To_Inner_Receiver()
	{
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage>());

		var sut = new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, "Test");
		await sut.ReceiveAsync(5, CancellationToken.None);

		A.CallTo(() => _innerReceiver.ReceiveAsync(5, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Throw_When_Meter_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new TelemetryTransportReceiver(_innerReceiver, null!, _activitySource, "Test"));
	}

	[Fact]
	public void Throw_When_ActivitySource_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new TelemetryTransportReceiver(_innerReceiver, _meter, null!, "Test"));
	}

	[Fact]
	public void Throw_When_TransportName_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new TelemetryTransportReceiver(_innerReceiver, _meter, _activitySource, null!));
	}

	private static TransportReceivedMessage CreateTestMessage() =>
		new()
		{
			Id = $"msg-{Guid.NewGuid():N}",
			Body = "test-body"u8.ToArray(),
			Source = "test-queue",
		};

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		(_innerReceiver as IDisposable)?.Dispose();
		_meterListener.Dispose();
		_meter.Dispose();
		_activitySource.Dispose();
	}
}
