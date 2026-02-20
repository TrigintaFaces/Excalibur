// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="DeadLetterTransportSubscriber"/>.
/// Verifies that rejected messages are routed to the dead letter handler and metrics are recorded.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DeadLetterTransportSubscriberShould : IDisposable
{
	private readonly ITransportSubscriber _innerSubscriber = A.Fake<ITransportSubscriber>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.DlqTest", "1.0.0");
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, long Value)> _recordedCounters = [];
	private readonly List<(TransportReceivedMessage Message, string? Reason)> _deadLetteredMessages = [];
	private bool _disposed;

	public DeadLetterTransportSubscriberShould()
	{
		A.CallTo(() => _innerSubscriber.Source).Returns("test-queue");

		_meterListener = new MeterListener();
		_meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == "Excalibur.Dispatch.Transport.DlqTest")
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
	public async Task Route_Rejected_Message_To_DeadLetterHandler()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			CancellationToken.None);

		_deadLetteredMessages.Count.ShouldBe(1);
		_deadLetteredMessages[0].Message.ShouldBeSameAs(testMessage);
	}

	[Fact]
	public async Task Not_Route_Acknowledged_Message_To_DeadLetterHandler()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			CancellationToken.None);

		_deadLetteredMessages.ShouldBeEmpty();
	}

	[Fact]
	public async Task Not_Route_Requeued_Message_To_DeadLetterHandler()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Requeue),
			CancellationToken.None);

		_deadLetteredMessages.ShouldBeEmpty();
	}

	[Fact]
	public async Task Extract_Error_Reason_From_Properties()
	{
		var testMessage = CreateTestMessage(new Dictionary<string, object>
		{
			["error.message"] = "Deserialization failed"
		});
		SetupInnerToInvokeHandler(testMessage);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			CancellationToken.None);

		_deadLetteredMessages.Count.ShouldBe(1);
		_deadLetteredMessages[0].Reason.ShouldBe("Deserialization failed");
	}

	[Fact]
	public async Task Pass_Null_Reason_When_No_Error_Property()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			CancellationToken.None);

		_deadLetteredMessages.Count.ShouldBe(1);
		_deadLetteredMessages[0].Reason.ShouldBeNull();
	}

	[Fact]
	public async Task Record_DeadLettered_Counter_On_Reject()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesDeadLettered && c.Value == 1);
	}

	[Fact]
	public async Task Not_Record_DeadLettered_Counter_On_Acknowledge()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			CancellationToken.None);

		_recordedCounters.ShouldNotContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesDeadLettered);
	}

	[Fact]
	public async Task Work_Without_Meter()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage);

		// Create without meter — should not throw
		var sut = new DeadLetterTransportSubscriber(
			_innerSubscriber,
			CaptureDeadLetterHandler,
			"test-transport");

		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			CancellationToken.None);

		_deadLetteredMessages.Count.ShouldBe(1);
	}

	[Fact]
	public void Throw_When_DeadLetterHandler_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new DeadLetterTransportSubscriber(_innerSubscriber, null!, "test-transport"));
	}

	[Fact]
	public void Throw_When_InnerSubscriber_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new DeadLetterTransportSubscriber(
				null!,
				CaptureDeadLetterHandler,
				"test-transport"));
	}

	[Fact]
	public void Throw_When_TransportName_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new DeadLetterTransportSubscriber(
				_innerSubscriber,
				CaptureDeadLetterHandler,
				null!));
	}

	[Fact]
	public async Task Record_TransportName_Tag_From_Constructor_Not_Source()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage);

		var recordedTags = new List<KeyValuePair<string, object?>>();
		_meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			_recordedCounters.Add((instrument.Name, measurement));
			foreach (var tag in tags)
			{
				recordedTags.Add(tag);
			}
		});

		var sut = new DeadLetterTransportSubscriber(
			_innerSubscriber, CaptureDeadLetterHandler, "Kafka", _meter);

		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			CancellationToken.None);

		recordedTags.ShouldContain(t =>
			t.Key == TransportTelemetryConstants.Tags.TransportName &&
			(string?)t.Value == "Kafka");
		recordedTags.ShouldContain(t =>
			t.Key == TransportTelemetryConstants.Tags.Source &&
			(string?)t.Value == "test-queue");
	}

	[Fact]
	public void Expose_Source_From_InnerSubscriber()
	{
		var sut = CreateSut();
		sut.Source.ShouldBe("test-queue");
	}

	[Fact]
	public async Task Delegate_SubscribeAsync_To_Inner()
	{
		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			CancellationToken.None);

		A.CallTo(() => _innerSubscriber.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Still_Return_Reject_Action_After_DLQ_Routing()
	{
		var testMessage = CreateTestMessage();
		MessageAction? capturedAction = null;

		// Setup inner to invoke handler and capture what the wrapper returns
		A.CallTo(() => _innerSubscriber.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var handler = call.GetArgument<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>(0);
				capturedAction = await handler(testMessage, CancellationToken.None).ConfigureAwait(false);
			});

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			CancellationToken.None);

		capturedAction.ShouldBe(MessageAction.Reject);
	}

	private DeadLetterTransportSubscriber CreateSut() =>
		new(_innerSubscriber, CaptureDeadLetterHandler, "test-transport", _meter);

	private Task CaptureDeadLetterHandler(TransportReceivedMessage message, string? reason, CancellationToken ct)
	{
		_deadLetteredMessages.Add((message, reason));
		return Task.CompletedTask;
	}

	private static TransportReceivedMessage CreateTestMessage(
		Dictionary<string, object>? properties = null) =>
		new()
		{
			Id = "msg-dlq-1",
			Body = "dead-letter-test"u8.ToArray(),
			Source = "test-queue",
			Properties = properties ?? new Dictionary<string, object>(StringComparer.Ordinal),
		};

	private void SetupInnerToInvokeHandler(TransportReceivedMessage message)
	{
		A.CallTo(() => _innerSubscriber.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var handler = call.GetArgument<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>(0);
				await handler(message, CancellationToken.None).ConfigureAwait(false);
			});
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_meterListener.Dispose();
		_meter.Dispose();
	}
}
