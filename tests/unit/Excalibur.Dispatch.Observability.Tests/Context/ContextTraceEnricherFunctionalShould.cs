// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Functional tests for <see cref="ContextTraceEnricher"/> verifying activity enrichment, baggage propagation, and context events.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "ContextFlow")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code only")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code only")]
public sealed class ContextTraceEnricherFunctionalShould : IDisposable
{
	private readonly ContextTraceEnricher _enricher;
	private readonly ActivityListener _listener;
	private readonly List<Activity> _activities = [];

	public ContextTraceEnricherFunctionalShould()
	{
		var sanitizer = A.Fake<ITelemetrySanitizer>();
		// Pass-through sanitizer: returns raw value for any tag
		A.CallTo(() => sanitizer.SanitizeTag(A<string>._, A<string?>._))
			.ReturnsLazily((string _, string? raw) => raw);

		_enricher = new ContextTraceEnricher(
			NullLogger<ContextTraceEnricher>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			sanitizer);

		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => _activities.Add(activity),
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		_enricher.Dispose();
		foreach (var activity in _activities)
		{
			activity.Dispose();
		}
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() => new ContextTraceEnricher(
			null!,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			A.Fake<ITelemetrySanitizer>()));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new ContextTraceEnricher(
			NullLogger<ContextTraceEnricher>.Instance,
			null!,
			A.Fake<ITelemetrySanitizer>()));
	}

	[Fact]
	public void ThrowOnNullSanitizer()
	{
		Should.Throw<ArgumentNullException>(() => new ContextTraceEnricher(
			NullLogger<ContextTraceEnricher>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			null!));
	}

	[Fact]
	public void EnrichActivity_WithStandardAttributes()
	{
		using var activity = new Activity("test.enrichment").Start();
		var context = CreateFakeContext("msg-1", "corr-1", tenantId: "tenant-1", userId: "user-1");

		_enricher.EnrichActivity(activity, context);

		activity.GetTagItem("message.id")?.ToString().ShouldBe("msg-1");
		activity.GetTagItem("correlation.id")?.ToString().ShouldBe("corr-1");
		activity.GetTagItem("tenant.id")?.ToString().ShouldBe("tenant-1");
		activity.GetTagItem("user.id")?.ToString().ShouldBe("user-1");
		activity.GetTagItem("message.type")?.ToString().ShouldBe("OrderCreated");
	}

	[Fact]
	public void EnrichActivity_HandlesNullActivity()
	{
		var context = CreateFakeContext("msg-2", "corr-2");

		// Should not throw
		_enricher.EnrichActivity(null, context);
	}

	[Fact]
	public void EnrichActivity_HandlesNullContext()
	{
		using var activity = new Activity("test.null-context").Start();

		// Should not throw
		_enricher.EnrichActivity(activity, null!);
	}

	[Fact]
	public void CreateContextOperationSpan()
	{
		var context = CreateFakeContext("msg-3", "corr-3");

		using var span = _enricher.CreateContextOperationSpan("Validate", context);

		span.ShouldNotBeNull();
		span.OperationName.ShouldBe("Context.Validate");
		span.GetTagItem("context.operation")?.ToString().ShouldBe("Validate");
		span.GetTagItem("message.id")?.ToString().ShouldBe("msg-3");
	}

	[Fact]
	public void LinkRelatedTrace_DoesNotThrowForValidInput()
	{
		using var activity = new Activity("test.link").Start();

		// Should not throw
		_enricher.LinkRelatedTrace(activity, "corr-link-1", "child");
	}

	[Fact]
	public void LinkRelatedTrace_HandlesNullActivity()
	{
		// Should not throw
		_enricher.LinkRelatedTrace(null, "corr-link-2");
	}

	[Fact]
	public void LinkRelatedTrace_HandlesNullCorrelationId()
	{
		using var activity = new Activity("test.link-null").Start();

		// Should not throw
		_enricher.LinkRelatedTrace(activity, null!);
	}

	[Fact]
	public void PropagateContextAsBaggage()
	{
		var context = CreateFakeContext("msg-bag", "corr-bag", tenantId: "t-1", userId: "u-1");
		var carrier = new Dictionary<string, string>();

		_enricher.PropagateContextAsBaggage(context, carrier);

		carrier.ShouldContainKey("correlation.id");
		carrier["correlation.id"].ShouldBe("corr-bag");
		carrier.ShouldContainKey("message.type");
	}

	[Fact]
	public void PropagateContextAsBaggage_ThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_enricher.PropagateContextAsBaggage(null!, new Dictionary<string, string>()));
	}

	[Fact]
	public void PropagateContextAsBaggage_ThrowsOnNullCarrier()
	{
		var context = CreateFakeContext("msg", "corr");
		Should.Throw<ArgumentNullException>(() =>
			_enricher.PropagateContextAsBaggage(context, null!));
	}

	[Fact]
	public void AddContextEvent_ThrowsOnNullEventName()
	{
		var context = CreateFakeContext("msg", "corr");
		Should.Throw<ArgumentNullException>(() =>
			_enricher.AddContextEvent(null!, context));
	}

	[Fact]
	public void AddContextEvent_ThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_enricher.AddContextEvent("test-event", null!));
	}

	[Fact]
	public void ExtractContextFromBaggage_HandlesNullInputs()
	{
		// Should not throw for null carrier or context
		_enricher.ExtractContextFromBaggage(null!, A.Fake<IMessageContext>());
		_enricher.ExtractContextFromBaggage(new Dictionary<string, string>(), null!);
	}

	[Fact]
	public void EnrichActivity_AddsDeliveryCount()
	{
		using var activity = new Activity("test.delivery").Start();
		var context = CreateFakeContext("msg-dc", "corr-dc");
		A.CallTo(() => context.DeliveryCount).Returns(3);

		_enricher.EnrichActivity(activity, context);

		activity.GetTagItem("message.delivery_count").ShouldBe(3);
	}

	[Fact]
	public void EnrichActivity_AddsTimestamps()
	{
		using var activity = new Activity("test.timestamps").Start();
		var now = DateTimeOffset.UtcNow;
		var context = CreateFakeContext("msg-ts", "corr-ts");
		A.CallTo(() => context.SentTimestampUtc).Returns(now);
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(now);

		_enricher.EnrichActivity(activity, context);

		activity.GetTagItem("message.sent_timestamp").ShouldNotBeNull();
		activity.GetTagItem("message.received_timestamp").ShouldNotBeNull();
	}

	private static IMessageContext CreateFakeContext(
		string? messageId = "msg-default",
		string? correlationId = "corr-default",
		string? tenantId = null,
		string? userId = null)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.MessageType).Returns("OrderCreated");
		A.CallTo(() => context.TenantId).Returns(tenantId);
		A.CallTo(() => context.UserId).Returns(userId);
		A.CallTo(() => context.CausationId).Returns(null);
		A.CallTo(() => context.ExternalId).Returns(null);
		A.CallTo(() => context.TraceParent).Returns(null);
		A.CallTo(() => context.Source).Returns(null);
		A.CallTo(() => context.ContentType).Returns(null);
		A.CallTo(() => context.DeliveryCount).Returns(1);
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.SentTimestampUtc).Returns(null);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}
}
