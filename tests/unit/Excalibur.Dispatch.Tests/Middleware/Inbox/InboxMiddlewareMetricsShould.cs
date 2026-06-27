// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Middleware.Inbox;

/// <summary>
/// Author≠impl regression lock for S851 Lane 5 · <c>lnekjc</c> (+ S852 MS-D · <c>b5lr6q</c>) —
/// <see cref="InboxMiddleware"/> must emit the <c>dispatch.inbox.processed</c> counter on every terminal
/// outcome (success / failure / error) tagged with <c>inbox.result</c> + <c>inbox.mode</c>, AND the
/// distinct <c>dispatch.inbox.deduplicated</c> counter on every dedup disposition (duplicate /
/// timeout-reset) tagged with <c>inbox.disposition</c> + <c>inbox.mode</c> (it emitted no metrics before
/// <c>lnekjc</c>; the deduplicated counter is new in <c>b5lr6q</c>).
/// </summary>
/// <remarks>
/// Authored independently of the implementer (BackendDeveloper/FrontendDeveloper). A <see cref="MeterListener"/>
/// captures measurements from the <c>Excalibur.Dispatch.Inbox</c> meter. <b>RED mutants:</b> remove the
/// <c>ProcessedCounter.Add(...)</c> call ⇒ every processed fact RED; remove the <c>RecordDeduplicated(...)</c>
/// calls ⇒ every deduplicated fact RED. b5lr6q is a DISTINCT counter from processed, so dedup-rate
/// (<c>deduplicated{duplicate} / processed</c>) is independently computable.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxMiddlewareMetricsShould
{
	private const string InboxMeterName = "Excalibur.Dispatch.Inbox";
	private const string ProcessedCounter = "dispatch.inbox.processed";
	private const string DeduplicatedCounter = "dispatch.inbox.deduplicated";

	[Fact]
	public async Task RecordProcessed_WithSuccessResult_OnHandlerSuccess()
	{
		var recorded = await CaptureAsync(ResultDelegate(succeeded: true));
		HasProcessed(recorded, result: "success").ShouldBeTrue();
	}

	[Fact]
	public async Task RecordProcessed_WithFailureResult_OnHandlerFailure()
	{
		var recorded = await CaptureAsync(ResultDelegate(succeeded: false));
		HasProcessed(recorded, result: "failure").ShouldBeTrue();
	}

	[Fact]
	public async Task RecordProcessed_WithErrorResult_OnHandlerException()
	{
		var recorded = await CaptureAsync((_, _, _) => throw new InvalidOperationException("boom"));
		HasProcessed(recorded, result: "error").ShouldBeTrue();
	}

	// ── b5lr6q (MS-D AC-D2): the distinct dispatch.inbox.deduplicated counter, all 3 dispositions ──

	[Fact]
	public async Task RecordDeduplicated_OnLightModeDuplicate()
	{
		var recorded = await CaptureAsync(CreateLightMiddleware(isDuplicate: true), SuccessDelegate());
		HasDeduplicated(recorded, disposition: "duplicate", mode: "light")
			.ShouldBeTrue("a light-mode duplicate must emit dispatch.inbox.deduplicated{duplicate,light}");
	}

	[Fact]
	public async Task RecordDeduplicated_OnFullModeAlreadyProcessed()
	{
		// An existing Processed entry ⇒ the message is a duplicate; the middleware short-circuits to Success.
		var entry = new InboxEntry { Status = InboxStatus.Processed };
		var recorded = await CaptureAsync(CreateFullMiddleware(entry), SuccessDelegate());
		HasDeduplicated(recorded, disposition: "duplicate", mode: "full")
			.ShouldBeTrue("a full-mode already-processed message must emit dispatch.inbox.deduplicated{duplicate,full}");
	}

	[Fact]
	public async Task RecordDeduplicated_OnFullModeProcessingTimeout()
	{
		// An entry stuck in Processing past ProcessingTimeout ⇒ reset to Received (timeout-reset disposition).
		var entry = new InboxEntry { Status = InboxStatus.Processing, LastAttemptAt = DateTimeOffset.UtcNow - TimeSpan.FromDays(1) };
		var recorded = await CaptureAsync(CreateFullMiddleware(entry), SuccessDelegate());
		HasDeduplicated(recorded, disposition: "timeout-reset", mode: "full")
			.ShouldBeTrue("a full-mode stuck-processing timeout must emit dispatch.inbox.deduplicated{timeout-reset,full}");
	}

	private static bool HasProcessed(IEnumerable<RecordedMeasurement> measurements, string result) =>
		measurements.Any(m =>
			m.Name == ProcessedCounter
			&& m.Value == 1
			&& m.Tags.Any(t => t.Key == "inbox.result" && (string?)t.Value == result)
			&& m.Tags.Any(t => t.Key == "inbox.mode" && (string?)t.Value == "light"));

	private static bool HasDeduplicated(IEnumerable<RecordedMeasurement> measurements, string disposition, string mode) =>
		measurements.Any(m =>
			m.Name == DeduplicatedCounter
			&& m.Value == 1
			&& m.Tags.Any(t => t.Key == "inbox.disposition" && (string?)t.Value == disposition)
			&& m.Tags.Any(t => t.Key == "inbox.mode" && (string?)t.Value == mode));

	private static DispatchRequestDelegate SuccessDelegate()
	{
		var result = A.Fake<IMessageResult>();
		_ = A.CallTo(() => result.Succeeded).Returns(true);
		return (_, _, _) => new ValueTask<IMessageResult>(result);
	}

	private static DispatchRequestDelegate ResultDelegate(bool succeeded)
	{
		var result = A.Fake<IMessageResult>();
		_ = A.CallTo(() => result.Succeeded).Returns(succeeded);
		_ = A.CallTo(() => result.ErrorMessage).Returns(succeeded ? null : "handler failed");
		return (_, _, _) => new ValueTask<IMessageResult>(result);
	}

	private static Task<List<RecordedMeasurement>> CaptureAsync(DispatchRequestDelegate next) =>
		CaptureAsync(CreateMiddleware(), next);

	private static async Task<List<RecordedMeasurement>> CaptureAsync(InboxMiddleware middleware, DispatchRequestDelegate next)
	{
		var recorded = new List<RecordedMeasurement>();
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, l) =>
		{
			if (instrument.Meter.Name == InboxMeterName)
			{
				l.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
			recorded.Add(new RecordedMeasurement(instrument.Name, value, tags.ToArray())));
		listener.Start();

		try
		{
			_ = await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(), CreateContext(), next, CancellationToken.None);
		}
		catch (InvalidOperationException)
		{
			// The error-path fact rethrows after recording the metric; swallow the rethrow.
		}

		return recorded;
	}

	private static InboxMiddleware CreateMiddleware() => CreateLightMiddleware(isDuplicate: false);

	private static InboxMiddleware CreateLightMiddleware(bool isDuplicate)
	{
		var deduplicator = A.Fake<IInMemoryDeduplicator>();
		_ = A.CallTo(() => deduplicator.IsDuplicateAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(isDuplicate);

		var options = Microsoft.Extensions.Options.Options.Create(new InboxConfigurationOptions { Enabled = true });
		return new InboxMiddleware(options, inboxStore: null, deduplicator, NullLogger<InboxMiddleware>.Instance);
	}

	private static InboxMiddleware CreateFullMiddleware(InboxEntry existingEntry)
	{
		var store = A.Fake<IInboxStore>();
		_ = A.CallTo(() => store.GetEntryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>(existingEntry));

		var options = Microsoft.Extensions.Options.Options.Create(new InboxConfigurationOptions { Enabled = true });
		return new InboxMiddleware(options, store, deduplicator: null, NullLogger<InboxMiddleware>.Instance);
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		_ = A.CallTo(() => context.MessageId).Returns("inbox-metric-msg");
		return context;
	}

	private sealed record RecordedMeasurement(string Name, long Value, KeyValuePair<string, object?>[] Tags);
}
