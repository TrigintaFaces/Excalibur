// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="DeadLetterTransportReceiver"/>.
/// Verifies that rejected messages are routed to the dead letter handler and metrics are recorded.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DeadLetterTransportReceiverShould : IDisposable
{
	private readonly ITransportReceiver _innerReceiver = A.Fake<ITransportReceiver>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.ReceiverDlqTest", "1.0.0");
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, long Value)> _recordedCounters = [];
	private readonly List<(TransportReceivedMessage Message, string? Reason)> _deadLetteredMessages = [];
	private bool _disposed;

	public DeadLetterTransportReceiverShould()
	{
		A.CallTo(() => _innerReceiver.Source).Returns("test-queue");

		_meterListener = new MeterListener();
		_meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == "Excalibur.Dispatch.Transport.ReceiverDlqTest")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			_recordedCounters.Add((instrument.Name, measurement));
		});

		_meterListener.Start();
	}

	[Fact]
	public async Task Route_To_DLQ_Handler_When_Requeue_Is_False()
	{
		var testMessage = CreateTestMessage();
		var sut = CreateSut();

		await sut.RejectAsync(testMessage, "bad message", false, CancellationToken.None);

		_deadLetteredMessages.Count.ShouldBe(1);
		_deadLetteredMessages[0].Message.ShouldBeSameAs(testMessage);
		_deadLetteredMessages[0].Reason.ShouldBe("bad message");
	}

	[Fact]
	public async Task Pass_Through_To_Inner_When_Requeue_Is_True()
	{
		var testMessage = CreateTestMessage();
		var sut = CreateSut();

		await sut.RejectAsync(testMessage, "retry", true, CancellationToken.None);

		_deadLetteredMessages.ShouldBeEmpty();
	}

	[Fact]
	public async Task Always_Call_Base_RejectAsync_Regardless_Of_Requeue()
	{
		var testMessage = CreateTestMessage();
		var sut = CreateSut();

		await sut.RejectAsync(testMessage, "reason", false, CancellationToken.None);

		A.CallTo(() => _innerReceiver.RejectAsync(testMessage, "reason", false, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Always_Call_Base_RejectAsync_When_Requeue_Is_True()
	{
		var testMessage = CreateTestMessage();
		var sut = CreateSut();

		await sut.RejectAsync(testMessage, "retry", true, CancellationToken.None);

		A.CallTo(() => _innerReceiver.RejectAsync(testMessage, "retry", true, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Record_DeadLettered_Metric_When_Routing_To_DLQ()
	{
		var testMessage = CreateTestMessage();
		var sut = CreateSut();

		await sut.RejectAsync(testMessage, "bad", false, CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesDeadLettered && c.Value == 1);
	}

	[Fact]
	public async Task Not_Record_Metric_When_Requeue_Is_True()
	{
		var testMessage = CreateTestMessage();
		var sut = CreateSut();

		await sut.RejectAsync(testMessage, "retry", true, CancellationToken.None);

		_recordedCounters.ShouldNotContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesDeadLettered);
	}

	[Fact]
	public async Task Record_TransportName_Tag_From_Constructor_Not_Source()
	{
		var testMessage = CreateTestMessage();

		var recordedTags = new List<KeyValuePair<string, object?>>();
		_meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			_recordedCounters.Add((instrument.Name, measurement));
			foreach (var tag in tags)
			{
				recordedTags.Add(tag);
			}
		});

		var sut = new DeadLetterTransportReceiver(
			_innerReceiver, CaptureDeadLetterHandler, "Kafka", _meter);

		await sut.RejectAsync(testMessage, "bad", false, CancellationToken.None);

		recordedTags.ShouldContain(t =>
			t.Key == TransportTelemetryConstants.Tags.TransportName &&
			(string?)t.Value == "Kafka");
		recordedTags.ShouldContain(t =>
			t.Key == TransportTelemetryConstants.Tags.Source &&
			(string?)t.Value == "test-queue");
	}

	[Fact]
	public async Task Work_Without_Meter()
	{
		var testMessage = CreateTestMessage();

		var sut = new DeadLetterTransportReceiver(
			_innerReceiver, CaptureDeadLetterHandler, "test-transport");

		await sut.RejectAsync(testMessage, "bad", false, CancellationToken.None);

		_deadLetteredMessages.Count.ShouldBe(1);
	}

	[Fact]
	public void Throw_When_DeadLetterHandler_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new DeadLetterTransportReceiver(_innerReceiver, null!, "test-transport"));
	}

	[Fact]
	public void Throw_When_TransportName_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new DeadLetterTransportReceiver(
				_innerReceiver, CaptureDeadLetterHandler, null!));
	}

	[Fact]
	public async Task ReceiveAsync_Passes_Through_Unchanged()
	{
		var messages = new List<TransportReceivedMessage> { CreateTestMessage() };
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(messages);

		var sut = CreateSut();
		var result = await sut.ReceiveAsync(10, CancellationToken.None);

		result.ShouldBeSameAs(messages);
	}

	[Fact]
	public async Task AcknowledgeAsync_Passes_Through_Unchanged()
	{
		var testMessage = CreateTestMessage();
		var sut = CreateSut();

		await sut.AcknowledgeAsync(testMessage, CancellationToken.None);

		A.CallTo(() => _innerReceiver.AcknowledgeAsync(testMessage, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private DeadLetterTransportReceiver CreateSut() =>
		new(_innerReceiver, CaptureDeadLetterHandler, "test-transport", _meter);

	private Task CaptureDeadLetterHandler(TransportReceivedMessage message, string? reason, CancellationToken ct)
	{
		_deadLetteredMessages.Add((message, reason));
		return Task.CompletedTask;
	}

	private static TransportReceivedMessage CreateTestMessage() =>
		new()
		{
			Id = "msg-dlq-1",
			Body = "dead-letter-test"u8.ToArray(),
			Source = "test-queue",
		};

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		(_innerReceiver as IDisposable)?.Dispose();
		_meterListener.Dispose();
		_meter.Dispose();
	}
}
