// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // ValueTask in FakeItEasy .Returns()

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.ErrorHandling.PoisonMessage;

/// <summary>
/// Author≠impl regression lock for S851 Lane 5 · <c>fypqgz</c> (+ S852 Lane D · <c>w4m560</c>) —
/// <see cref="PoisonMessageMiddleware"/> must emit the <c>dispatch.poison.dead_lettered</c> counter when a
/// poison message is moved to the dead-letter queue, tagged with <c>poison.detector</c> and a
/// <b>low-cardinality</b> <c>poison.reason</c> drawn from the bounded
/// <see cref="DeadLetterReason"/> enum (it emitted no metric before <c>fypqgz</c>).
/// </summary>
/// <remarks>
/// Authored independently of the implementer (BackendDeveloper; reassigned from FrontendDeveloper) against
/// committed mainline. A <see cref="MeterListener"/> captures the
/// <c>Excalibur.Dispatch.PoisonMessage.Middleware</c> meter. The handler runs only when the detector flags
/// the message poison (after a downstream failure). <b>RED mutant:</b> remove the
/// <c>DeadLetteredCounter.Add(...)</c> call ⇒ no measurement ⇒ RED. The negative fact proves non-vacuity:
/// a non-poison failure re-throws and emits nothing.
/// <para>
/// <b>S852 <c>w4m560</c> cardinality bound (F-5 strengthen-don't-weaken flip):</b> the <c>poison.reason</c>
/// metric tag MUST carry the bounded <see cref="DeadLetterReason.PoisonMessage"/> enum name, NOT the
/// free-form <c>detectionResult.Reason</c> (which custom detectors can fill with per-message text → unbounded
/// tag cardinality / observability-backend soft-DoS). The rich free-form reason is preserved on the Activity
/// span only. The test deliberately feeds a free-form reason (<c>"bad payload"</c>) and asserts the metric
/// tag is the bounded enum value — proving the free-form text is never leaked into the metric.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PoisonMessageMiddlewareMetricsShould
{
	// Bind to the same const the middleware publishes on (rename-proof). Meters.PoisonMessage resolves to
	// "Excalibur.Dispatch.PoisonMessage.Middleware" — the dead-letter routing meter.
	private const string PoisonMeterName = DispatchTelemetryConstants.Meters.PoisonMessage;
	private const string DeadLetteredCounter = "dispatch.poison.dead_lettered";

	[Fact]
	public async Task RecordDeadLettered_WhenPoisonMovedToDeadLetterQueue()
	{
		var recorded = await CaptureAsync(
			PoisonDetectionResult.Poison(reason: "bad payload", detectorName: "TestDetector"),
			expectThrow: false);

		// w4m560: the free-form detection reason ("bad payload") must NOT appear in the metric tag —
		// the tag carries the bounded DeadLetterReason enum name (fully qualified to avoid the
		// Tests.Shared Azure-stub DeadLetterReason collision). Free-form text stays on the span only.
		recorded.Any(m =>
			m.Name == DeadLetteredCounter
			&& m.Value == 1
			&& m.Tags.Any(t => t.Key == "poison.detector" && (string?)t.Value == "TestDetector")
			&& m.Tags.Any(t => t.Key == "poison.reason"
				&& (string?)t.Value == nameof(Excalibur.Dispatch.ErrorHandling.DeadLetterReason.PoisonMessage)))
			.ShouldBeTrue("dead-lettering a poison message must emit dispatch.poison.dead_lettered with a bounded poison.reason");
	}

	[Fact]
	public async Task NotRecordDeadLettered_WhenFailureIsNotPoison()
	{
		// Not poison ⇒ the middleware re-throws and never routes to the DLQ, so the counter must not fire.
		var recorded = await CaptureAsync(PoisonDetectionResult.NotPoison(), expectThrow: true);

		recorded.Any(m => m.Name == DeadLetteredCounter).ShouldBeFalse();
	}

	private static async Task<List<RecordedMeasurement>> CaptureAsync(PoisonDetectionResult detection, bool expectThrow)
	{
		var recorded = new List<RecordedMeasurement>();
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, l) =>
		{
			if (instrument.Meter.Name == PoisonMeterName)
			{
				l.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
			recorded.Add(new RecordedMeasurement(instrument.Name, value, tags.ToArray())));
		listener.Start();

		using var middleware = CreateMiddleware(detection);

		async Task Invoke() =>
			_ = await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(),
				CreateContext(),
				(_, _, _) => throw new InvalidOperationException("downstream failure"),
				CancellationToken.None);

		if (expectThrow)
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(Invoke);
		}
		else
		{
			await Invoke();
		}

		return recorded;
	}

	private static PoisonMessageMiddleware CreateMiddleware(PoisonDetectionResult detection)
	{
		var detector = A.Fake<IPoisonMessageDetector>();
		_ = A.CallTo(() => detector.IsPoisonMessageAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception>._))
			.Returns(detection);

		var handler = A.Fake<IPoisonMessageHandler>(); // HandlePoisonMessageAsync defaults to a completed Task.

		var options = Microsoft.Extensions.Options.Options.Create(new PoisonMessageOptions { Enabled = true });
		return new PoisonMessageMiddleware(detector, handler, options, NullLogger<PoisonMessageMiddleware>.Instance);
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		_ = A.CallTo(() => context.MessageId).Returns("poison-metric-msg");
		return context;
	}

	private sealed record RecordedMeasurement(string Name, long Value, KeyValuePair<string, object?>[] Tags);
}
