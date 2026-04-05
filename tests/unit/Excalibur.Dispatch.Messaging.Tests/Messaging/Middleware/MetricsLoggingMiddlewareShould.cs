// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
///     Tests for the <see cref="MetricsLoggingMiddleware" /> class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class MetricsLoggingMiddlewareShould
{
	private sealed record TestMessage(string Value) : IDispatchMessage;

	private sealed class SizeProbeMessage : IDispatchMessage
	{
		private static int _probeReadCount;

		public int Probe => System.Threading.Interlocked.Increment(ref _probeReadCount);

		public static int ProbeReadCount => System.Threading.Volatile.Read(ref _probeReadCount);

		public static void ResetProbeCount() => System.Threading.Interlocked.Exchange(ref _probeReadCount, 0);
	}

	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new MetricsLoggingMiddleware(
				null!,
				A.Fake<IMessageMetrics>(),
				A.Fake<ITelemetrySanitizer>(),
				NullLogger<MetricsLoggingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullMessageMetrics() =>
		Should.Throw<ArgumentNullException>(() =>
			new MetricsLoggingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
				null!,
				A.Fake<ITelemetrySanitizer>(),
				NullLogger<MetricsLoggingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullSanitizer() =>
		Should.Throw<ArgumentNullException>(() =>
			new MetricsLoggingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
				A.Fake<IMessageMetrics>(),
				null!,
				NullLogger<MetricsLoggingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new MetricsLoggingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
				A.Fake<IMessageMetrics>(),
				A.Fake<ITelemetrySanitizer>(),
				null!));

	[Fact]
	public void CreateSuccessfully()
	{
		var sut = new MetricsLoggingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void HaveMetricsStage()
	{
		var sut = new MetricsLoggingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		sut.Stage.ShouldBe(DispatchMiddlewareStage.End);
	}

	[Fact]
	public void ApplyToAllMessages()
	{
		var sut = new MetricsLoggingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		sut.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
	}

	[Fact]
	public void ImplementIDispatchMiddleware()
	{
		var sut = new MetricsLoggingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		sut.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	[Fact]
	public async Task SkipMetricsCollectionForBypassedMessageType()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions
		{
			Enabled = true,
			BypassMetricsForTypes = [nameof(TestMessage)],
			RecordCustomMetrics = true,
		});
		var messageMetrics = A.Fake<IMessageMetrics>();
		var sut = new MetricsLoggingMiddleware(
			options,
			messageMetrics,
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		var message = new TestMessage("value");
		var context = A.Fake<IMessageContext>();
		var result = await sut.InvokeAsync(
			message,
			context,
			static (_, _, _) => ValueTask.FromResult(MessageResult.Success()),
			CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => messageMetrics.RecordMessageProcessedAsync(
				A<object>._,
				A<TimeSpan>._,
				A<bool>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SetMessageSizeToZeroWhenIncludeMessageSizesIsDisabled()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions
		{
			Enabled = true,
			IncludeMessageSizes = false,
			RecordOpenTelemetryMetrics = false,
			RecordCustomMetrics = true,
			LogProcessingDetails = false,
		});
		var messageMetrics = A.Fake<IMessageMetrics>();
		object? capturedContext = null;
		_ = A.CallTo(() => messageMetrics.RecordMessageProcessedAsync(
				A<object>._,
				A<TimeSpan>._,
				A<bool>._,
				A<CancellationToken>._))
			.Invokes(call => capturedContext = call.GetArgument<object>(0))
			.Returns(Task.CompletedTask);

		var sut = new MetricsLoggingMiddleware(
			options,
			messageMetrics,
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		var message = new TestMessage("value");
		var context = A.Fake<IMessageContext>();
		_ = await sut.InvokeAsync(
			message,
			context,
			static (_, _, _) => ValueTask.FromResult(MessageResult.Success()),
			CancellationToken.None);

		capturedContext.ShouldNotBeNull();
		var messageSizeProperty = capturedContext!.GetType().GetProperty("MessageSize");
		messageSizeProperty.ShouldNotBeNull();
		var messageSizeValue = messageSizeProperty!.GetValue(capturedContext);
		messageSizeValue.ShouldBeOfType<long>();
		((long)messageSizeValue).ShouldBe(0);
	}

	[Fact]
	public async Task SkipMetricsCollectionWhenSampleRateIsZero()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions
		{
			Enabled = true,
			SampleRate = 0d,
			RecordOpenTelemetryMetrics = false,
			RecordCustomMetrics = true,
			LogProcessingDetails = false,
		});
		var messageMetrics = A.Fake<IMessageMetrics>();
		var sut = new MetricsLoggingMiddleware(
			options,
			messageMetrics,
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		var message = new TestMessage("value");
		var context = A.Fake<IMessageContext>();
		var nextCalled = false;
		var result = await sut.InvokeAsync(
			message,
			context,
			(_, _, _) =>
			{
				nextCalled = true;
				return ValueTask.FromResult(MessageResult.Success());
			},
			CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		nextCalled.ShouldBeTrue();
		A.CallTo(() => messageMetrics.RecordMessageProcessedAsync(
				A<object>._,
				A<TimeSpan>._,
				A<bool>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SkipMetricsCollectionWhenSampleRateIsNaN()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions
		{
			Enabled = true,
			SampleRate = double.NaN,
			RecordOpenTelemetryMetrics = false,
			RecordCustomMetrics = true,
			LogProcessingDetails = false,
		});
		var messageMetrics = A.Fake<IMessageMetrics>();
		var sut = new MetricsLoggingMiddleware(
			options,
			messageMetrics,
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		var result = await sut.InvokeAsync(
			new TestMessage("value"),
			A.Fake<IMessageContext>(),
			static (_, _, _) => ValueTask.FromResult(MessageResult.Success()),
			CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => messageMetrics.RecordMessageProcessedAsync(
				A<object>._,
				A<TimeSpan>._,
				A<bool>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task CollectMetricsWhenSampleRateIsOne()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions
		{
			Enabled = true,
			SampleRate = 1d,
			RecordOpenTelemetryMetrics = false,
			RecordCustomMetrics = true,
			LogProcessingDetails = false,
		});
		var messageMetrics = A.Fake<IMessageMetrics>();
		_ = A.CallTo(() => messageMetrics.RecordMessageProcessedAsync(
				A<object>._,
				A<TimeSpan>._,
				A<bool>._,
				A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var sut = new MetricsLoggingMiddleware(
			options,
			messageMetrics,
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		var result = await sut.InvokeAsync(
			new TestMessage("value"),
			A.Fake<IMessageContext>(),
			static (_, _, _) => ValueTask.FromResult(MessageResult.Success()),
			CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => messageMetrics.RecordMessageProcessedAsync(
				A<object>._,
				A<TimeSpan>._,
				A<bool>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipMessageSizeEstimationWhenSizeIsNotConsumedAndNoActivity()
	{
		SizeProbeMessage.ResetProbeCount();

		var options = Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions
		{
			Enabled = true,
			SampleRate = 1d,
			IncludeMessageSizes = true,
			RecordOpenTelemetryMetrics = false,
			RecordCustomMetrics = false,
			LogProcessingDetails = false,
		});

		var sut = new MetricsLoggingMiddleware(
			options,
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		var result = await sut.InvokeAsync(
			new SizeProbeMessage(),
			A.Fake<IMessageContext>(),
			static (_, _, _) => ValueTask.FromResult(MessageResult.Success()),
			CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		SizeProbeMessage.ProbeReadCount.ShouldBe(0);
	}

	[Fact]
	public async Task EstimateMessageSizeWhenActivityIsPresentEvenWithoutOtherConsumers()
	{
		SizeProbeMessage.ResetProbeCount();

		var options = Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions
		{
			Enabled = true,
			SampleRate = 1d,
			IncludeMessageSizes = true,
			RecordOpenTelemetryMetrics = false,
			RecordCustomMetrics = false,
			LogProcessingDetails = false,
		});

		var sut = new MetricsLoggingMiddleware(
			options,
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		using var activity = new System.Diagnostics.Activity("metrics-size-probe").Start();
		var result = await sut.InvokeAsync(
			new SizeProbeMessage(),
			A.Fake<IMessageContext>(),
			static (_, _, _) => ValueTask.FromResult(MessageResult.Success()),
			CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		SizeProbeMessage.ProbeReadCount.ShouldBeGreaterThan(0);
		activity.GetTagItem("dispatch.messaging.message_size").ShouldNotBeNull();
	}
}
