// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Middleware.Inbox;

/// <summary>
/// Author≠impl regression lock for S851 Lane 1 · <c>bhwl6e</c> — the inbox consumer span must
/// <b>continue the producer's distributed trace</b> by re-parenting on the transmitted W3C
/// <c>traceparent</c>, instead of starting a new root (which severs the trace at receive).
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (BackendDeveloper), against the committed
/// <see cref="InboxMiddleware"/> surface. An <see cref="ActivityListener"/> (sampling
/// <see cref="ActivitySamplingResult.AllData"/> so <c>StartActivity</c> actually materializes the span)
/// captures the <c>inbox.process</c> activity the middleware starts.
/// </para>
/// <para>
/// <b>RED on the pre-fix surface:</b> the consumer span was created as <c>StartActivity("inbox.process",
/// Consumer)</c> with NO parent → a fresh random root <see cref="ActivityTraceId"/> ≠ the transmitted
/// trace, so <see cref="ReparentConsumerSpan_OnTransmittedTraceparent"/> goes RED. Fail-open (missing /
/// malformed traceparent ⇒ root span, never throw) is locked too (ADR-337 §4).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxMiddlewareTraceReparentShould
{
	// A syntactically valid W3C traceparent: trace-id 0af76519…319c, span-id b7ad6b71…3331.
	private const string TransmittedTraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
	private const string TransmittedTraceId = "0af7651916cd43dd8448eb211c80319c";
	private const string TransmittedSpanId = "b7ad6b7169203331";
	private const string RootSpanId = "0000000000000000";

	[Fact]
	public async Task ReparentConsumerSpan_OnTransmittedTraceparent()
	{
		var activity = await CaptureInboxActivityAsync(CreateContext(TransmittedTraceParent));

		_ = activity.ShouldNotBeNull("the inbox.process consumer span should have been started");
		// Re-parented onto the producer trace: same trace-id, parented to the producer's span.
		activity.TraceId.ToString().ShouldBe(TransmittedTraceId);
		activity.ParentSpanId.ToString().ShouldBe(TransmittedSpanId);
	}

	[Fact]
	public async Task StartRootSpan_WhenNoTraceparent_FailOpen()
	{
		var activity = await CaptureInboxActivityAsync(CreateContext(traceParent: null));

		_ = activity.ShouldNotBeNull();
		// No transmitted trace ⇒ a fresh root (not parented), and certainly not the producer trace.
		activity.ParentSpanId.ToString().ShouldBe(RootSpanId);
		activity.TraceId.ToString().ShouldNotBe(TransmittedTraceId);
	}

	[Fact]
	public async Task StartRootSpan_WhenMalformedTraceparent_FailOpen()
	{
		Activity? activity = null;

		// Malformed traceparent must NOT throw (ActivityContext.TryParse fails → default → root span).
		await Should.NotThrowAsync(async () =>
			activity = await CaptureInboxActivityAsync(CreateContext("not-a-valid-traceparent")));

		_ = activity.ShouldNotBeNull();
		activity.ParentSpanId.ToString().ShouldBe(RootSpanId);
		activity.TraceId.ToString().ShouldNotBe(TransmittedTraceId);
	}

	private static async Task<Activity?> CaptureInboxActivityAsync(IMessageContext context)
	{
		Activity? captured = null;
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Excalibur.Dispatch.Inbox",
			Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = a =>
			{
				if (a.OperationName == "inbox.process")
				{
					captured = a;
				}
			},
		};
		ActivitySource.AddActivityListener(listener);

		var success = A.Fake<IMessageResult>();
		_ = A.CallTo(() => success.Succeeded).Returns(true);

		_ = await CreateMiddleware().InvokeAsync(
			A.Fake<IDispatchMessage>(),
			context,
			(_, _, _) => new ValueTask<IMessageResult>(success),
			CancellationToken.None);

		return captured;
	}

	private static InboxMiddleware CreateMiddleware()
	{
		var deduplicator = A.Fake<IInMemoryDeduplicator>();
		// Not a duplicate ⇒ light-mode processing proceeds to the handler (and the span is started first).
		_ = A.CallTo(() => deduplicator.IsDuplicateAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(false);

		var options = Microsoft.Extensions.Options.Options.Create(new InboxConfigurationOptions { Enabled = true });
		return new InboxMiddleware(options, inboxStore: null, deduplicator, NullLogger<InboxMiddleware>.Instance);
	}

	private static IMessageContext CreateContext(string? traceParent)
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		_ = A.CallTo(() => context.MessageId).Returns("inbox-msg-1");

		if (traceParent is not null)
		{
			context.GetOrCreateIdentityFeature().TraceParent = traceParent;
		}

		return context;
	}
}
