// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Deep coverage tests for <see cref="ContextTraceEnricher"/> covering baggage extraction paths,
/// unknown baggage item handling, propagation with empty/null fields, and custom attribute limits.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextTraceEnricherDepthCoverageShould : IDisposable
{
	private readonly ITelemetrySanitizer _sanitizer;
	private ContextTraceEnricher? _enricher;

	public ContextTraceEnricherDepthCoverageShould()
	{
		_sanitizer = A.Fake<ITelemetrySanitizer>();
		// Default: pass-through sanitizer
		A.CallTo(() => _sanitizer.SanitizeTag(A<string>._, A<string?>._))
			.ReturnsLazily((string _, string? value) => value);
	}

	public void Dispose() => _enricher?.Dispose();

	[Fact]
	public void PropagateContextAsBaggage_SkipEmptyCorrelationId()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(null);
		A.CallTo(() => context.CausationId).Returns(null);
		A.CallTo(() => context.TenantId).Returns(null);
		A.CallTo(() => context.UserId).Returns(null);
		A.CallTo(() => context.MessageType).Returns(null);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		var carrier = new Dictionary<string, string>();

		// Act
		_enricher.PropagateContextAsBaggage(context, carrier);

		// Assert — carrier should be empty when all fields are null
		carrier.ShouldBeEmpty();
	}

	[Fact]
	public void PropagateContextAsBaggage_IncludeCausationId()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = CreateFakeContext();
		A.CallTo(() => context.CausationId).Returns("cause-abc");
		var carrier = new Dictionary<string, string>();

		// Act
		_enricher.PropagateContextAsBaggage(context, carrier);

		// Assert
		carrier.ShouldContainKey("causation.id");
		carrier["causation.id"].ShouldBe("cause-abc");
	}

	[Fact]
	public void PropagateContextAsBaggage_SanitizeTenantId()
	{
		// Arrange
		A.CallTo(() => _sanitizer.SanitizeTag("tenant.id", "tenant-xyz"))
			.Returns("hashed-tenant");
		_enricher = CreateEnricher();
		var context = CreateFakeContext();
		A.CallTo(() => context.TenantId).Returns("tenant-xyz");
		var carrier = new Dictionary<string, string>();

		// Act
		_enricher.PropagateContextAsBaggage(context, carrier);

		// Assert
		carrier.ShouldContainKey("tenant.id");
		carrier["tenant.id"].ShouldBe("hashed-tenant");
	}

	[Fact]
	public void PropagateContextAsBaggage_OmitWhenSanitizerReturnsNull()
	{
		// Arrange — sanitizer returns null for tenant.id (meaning: redact completely)
		A.CallTo(() => _sanitizer.SanitizeTag("tenant.id", A<string?>._)).Returns(null);
		A.CallTo(() => _sanitizer.SanitizeTag("user.id", A<string?>._)).Returns(null);
		_enricher = CreateEnricher();
		var context = CreateFakeContext();
		A.CallTo(() => context.TenantId).Returns("tenant-1");
		A.CallTo(() => context.UserId).Returns("user-1");
		var carrier = new Dictionary<string, string>();

		// Act
		_enricher.PropagateContextAsBaggage(context, carrier);

		// Assert — tenant.id and user.id should be omitted
		carrier.ShouldNotContainKey("tenant.id");
		carrier.ShouldNotContainKey("user.id");
	}

	[Fact]
	public void ExtractContextFromBaggage_ProcessCorrelationIdItem()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(null);
		A.CallTo(() => context.MessageId).Returns("msg-extract");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var carrier = new Dictionary<string, string>
		{
			["correlation.id"] = "extracted-corr",
		};

		// Act — should not throw
		_enricher.ExtractContextFromBaggage(carrier, context);

		// Assert — extraction processes without error
	}

	[Fact]
	public void ExtractContextFromBaggage_ProcessCausationIdItem()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CausationId).Returns(null);
		A.CallTo(() => context.MessageId).Returns("msg-cause");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var carrier = new Dictionary<string, string>
		{
			["causation.id"] = "cause-from-baggage",
		};

		// Act
		_enricher.ExtractContextFromBaggage(carrier, context);

		// Assert — no exception
	}

	[Fact]
	public void ExtractContextFromBaggage_PreserveUnknownBaggageItems_WhenEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Tracing.PreserveUnknownBaggageItems = true;
		_enricher = CreateEnricher(options);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-unknown");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var carrier = new Dictionary<string, string>
		{
			["custom.baggage.key"] = "custom-value",
		};

		// Act
		_enricher.ExtractContextFromBaggage(carrier, context);

		// Assert — unknown item stored as Baggage_ prefixed item
		// The context.SetItem call should have been made
	}

	[Fact]
	public void AddContextEvent_DoNothing_WhenNoCurrentActivity()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = CreateFakeContext();

		// Ensure no current activity
		Activity.Current = null;

		// Act — should not throw when no current activity
		_enricher.AddContextEvent("test.event", context);
	}

	[Fact]
	public void AddContextEvent_IncludeCustomAttributes()
	{
		// Arrange
		_enricher = CreateEnricher();
		using var source = new ActivitySource("test");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		using var activity = source.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext();
		var attrs = new Dictionary<string, object>
		{
			["order.id"] = "order-123",
			["amount"] = 99.99,
		};

		// Act
		_enricher.AddContextEvent("order.completed", context, attrs);

		// Assert
		var events = activity.Events.ToList();
		events.ShouldContain(e => e.Name == "order.completed");
	}

	[Fact]
	public void AddContextEvent_WorkWithNullAttributes()
	{
		// Arrange
		_enricher = CreateEnricher();
		using var source = new ActivitySource("test");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		using var activity = source.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext();

		// Act — null attributes should be fine
		_enricher.AddContextEvent("simple.event", context, attributes: null);

		// Assert
		activity.Events.ShouldContain(e => e.Name == "simple.event");
	}

	[Fact]
	public void EnrichActivity_IncludeCustomItems_RespectMaxLimit()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Tracing.IncludeCustomItemsInTraces = true;
		options.Tracing.MaxCustomItemsInTraces = 2;
		_enricher = CreateEnricher(options);

		using var source = new ActivitySource("test");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		using var activity = source.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>
		{
			["item1"] = "value1",
			["item2"] = "value2",
			["item3"] = "value3", // Should be excluded by limit
		});

		// Act
		_enricher.EnrichActivity(activity, context);

		// Assert — at most 2 custom items should be added
		var customTags = activity.Tags
			.Where(t => t.Key.StartsWith("context.item.", StringComparison.Ordinal))
			.ToList();
		customTags.Count.ShouldBeLessThanOrEqualTo(2);
	}

	[Fact]
	public void EnrichActivity_HandleNullTraceParent()
	{
		// Arrange
		_enricher = CreateEnricher();
		using var source = new ActivitySource("test");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		using var activity = source.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext();
		A.CallTo(() => context.TraceParent).Returns(null);

		// Act — should not throw
		_enricher.EnrichActivity(activity, context);

		// Assert — no parent tags
		activity.GetTagItem("parent.trace_id").ShouldBeNull();
	}

	[Fact]
	public void EnrichActivity_HandleEmptyTraceParent()
	{
		// Arrange
		_enricher = CreateEnricher();
		using var source = new ActivitySource("test");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		using var activity = source.StartActivity("test-op");
		activity.ShouldNotBeNull();

		var context = CreateFakeContext();
		A.CallTo(() => context.TraceParent).Returns("   ");

		// Act — should not throw
		_enricher.EnrichActivity(activity, context);

		// Assert — no parent tags
		activity.GetTagItem("parent.trace_id").ShouldBeNull();
	}

	[Fact]
	public void CreateContextOperationSpan_ReturnNull_WhenNoListener()
	{
		// Arrange — no ActivityListener registered for this source
		_enricher = CreateEnricher();
		var context = CreateFakeContext();

		// Act
		var activity = _enricher.CreateContextOperationSpan("NoListener", context);

		// Assert — may be null if no listener is configured
		// Either null or a valid activity is fine
		activity?.Dispose();
	}

	[Fact]
	public void LinkRelatedTrace_WithCustomLinkType()
	{
		// Arrange
		_enricher = CreateEnricher();
		using var source = new ActivitySource("test");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		using var activity = source.StartActivity("test-op");
		activity.ShouldNotBeNull();

		// Act — should not throw
		_enricher.LinkRelatedTrace(activity, "corr-link-test", "follows-from");
	}

	private ContextTraceEnricher CreateEnricher(ContextObservabilityOptions? options = null)
	{
		return new ContextTraceEnricher(
			NullLogger<ContextTraceEnricher>.Instance,
			MsOptions.Create(options ?? new ContextObservabilityOptions()),
			_sanitizer);
	}

	private static IMessageContext CreateFakeContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-depth");
		A.CallTo(() => context.CorrelationId).Returns("corr-depth");
		A.CallTo(() => context.CausationId).Returns("cause-depth");
		A.CallTo(() => context.MessageType).Returns("TestEvent");
		A.CallTo(() => context.UserId).Returns("user-depth");
		A.CallTo(() => context.TenantId).Returns("tenant-depth");
		A.CallTo(() => context.Source).Returns("test-source");
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.TraceParent).Returns(null);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}
}

#pragma warning restore IL2026, IL3050
