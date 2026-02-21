// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="TelemetryTransportSubscriber"/>.
/// Verifies OpenTelemetry metrics and tracing for subscription lifecycle and per-message handler.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TelemetryTransportSubscriberShould : IDisposable
{
	private readonly ITransportSubscriber _innerSubscriber = A.Fake<ITransportSubscriber>();
	private readonly Meter _meter = new("Excalibur.Dispatch.Transport.SubscriberTest", "1.0.0");
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Transport.SubscriberTest");
	private readonly MeterListener _meterListener;
	private readonly List<(string Name, long Value)> _recordedCounters = [];
	private readonly List<(string Name, double Value)> _recordedHistograms = [];
	private bool _disposed;

	public TelemetryTransportSubscriberShould()
	{
		A.CallTo(() => _innerSubscriber.Source).Returns("test-subscription");

		// Configure inner SubscribeAsync to immediately invoke the handler with a test message,
		// so metrics are actually recorded during the test.
		A.CallTo(() => _innerSubscriber.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.Invokes(call =>
			{
				// Default: do nothing. Tests override per-test as needed.
			})
			.Returns(Task.CompletedTask);

		_meterListener = new MeterListener();
		_meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == "Excalibur.Dispatch.Transport.SubscriberTest")
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

		_meterListener.Start();
	}

	[Fact]
	public async Task Record_ReceivedCounter_When_Handler_Invoked()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage, MessageAction.Acknowledge);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesReceived && c.Value == 1);
	}

	[Fact]
	public async Task Record_AcknowledgedCounter_On_Acknowledge()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage, MessageAction.Acknowledge);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesAcknowledged && c.Value == 1);
	}

	[Fact]
	public async Task Record_RejectedCounter_On_Reject()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage, MessageAction.Reject);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesRejected && c.Value == 1);
	}

	[Fact]
	public async Task Record_RequeuedCounter_On_Requeue()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage, MessageAction.Requeue);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Requeue),
			CancellationToken.None);

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.MessagesRequeued && c.Value == 1);
	}

	[Fact]
	public async Task Record_HandlerErrorCounter_On_Exception()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandlerThrowing(testMessage);

		var sut = CreateSut();

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SubscribeAsync(
				(_, _) => throw new InvalidOperationException("boom"),
				CancellationToken.None));

		_recordedCounters.ShouldContain(c =>
			c.Name == TransportTelemetryConstants.MetricNames.HandlerErrors && c.Value == 1);
	}

	[Fact]
	public async Task Record_HandlerDuration_On_Successful_Handler()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandler(testMessage, MessageAction.Acknowledge);

		var sut = CreateSut();
		await sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			CancellationToken.None);

		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.HandlerDuration && h.Value >= 0);
	}

	[Fact]
	public async Task Record_HandlerDuration_On_Handler_Error()
	{
		var testMessage = CreateTestMessage();
		SetupInnerToInvokeHandlerThrowing(testMessage);

		var sut = CreateSut();

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SubscribeAsync(
				(_, _) => throw new InvalidOperationException("boom"),
				CancellationToken.None));

		_recordedHistograms.ShouldContain(h =>
			h.Name == TransportTelemetryConstants.MetricNames.HandlerDuration && h.Value >= 0);
	}

	[Fact]
	public void Expose_Source_From_InnerSubscriber()
	{
		var sut = CreateSut();
		sut.Source.ShouldBe("test-subscription");
	}

	[Fact]
	public void Throw_When_Meter_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new TelemetryTransportSubscriber(_innerSubscriber, null!, _activitySource, "Test"));
	}

	[Fact]
	public void Throw_When_ActivitySource_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new TelemetryTransportSubscriber(_innerSubscriber, _meter, null!, "Test"));
	}

	[Fact]
	public void Throw_When_TransportName_Is_Null()
	{
		Should.Throw<ArgumentException>(
			() => new TelemetryTransportSubscriber(_innerSubscriber, _meter, _activitySource, null!));
	}

	[Fact]
	public void Throw_When_TransportName_Is_Empty()
	{
		Should.Throw<ArgumentException>(
			() => new TelemetryTransportSubscriber(_innerSubscriber, _meter, _activitySource, ""));
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

	private TelemetryTransportSubscriber CreateSut() =>
		new(_innerSubscriber, _meter, _activitySource, "Test");

	private static TransportReceivedMessage CreateTestMessage() =>
		new()
		{
			Id = "msg-1",
			Body = "hello"u8.ToArray(),
			Source = "test-subscription",
		};

	private void SetupInnerToInvokeHandler(TransportReceivedMessage message, MessageAction action)
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

	private void SetupInnerToInvokeHandlerThrowing(TransportReceivedMessage message)
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
		(_innerSubscriber as IDisposable)?.Dispose();
		_meterListener.Dispose();
		_meter.Dispose();
		_activitySource.Dispose();
	}
}
