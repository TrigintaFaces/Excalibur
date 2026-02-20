// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// In-depth unit tests for <see cref="ContextTraceEnricher"/> covering uncovered code paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextTraceEnricherDepthShould : IDisposable
{
	private readonly ITelemetrySanitizer _fakeSanitizer;
	private ContextTraceEnricher? _enricher;

	public ContextTraceEnricherDepthShould()
	{
		_fakeSanitizer = A.Fake<ITelemetrySanitizer>();
		// Default: sanitizer passes through values
		A.CallTo(() => _fakeSanitizer.SanitizeTag(A<string>._, A<string?>._))
			.ReturnsLazily((string _, string? value) => value);
	}

	public void Dispose() => _enricher?.Dispose();

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextTraceEnricher(
				null!,
				MsOptions.Create(new ContextObservabilityOptions()),
				_fakeSanitizer));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextTraceEnricher(
				NullLogger<ContextTraceEnricher>.Instance,
				null!,
				_fakeSanitizer));
	}

	[Fact]
	public void ThrowOnNullSanitizer()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextTraceEnricher(
				NullLogger<ContextTraceEnricher>.Instance,
				MsOptions.Create(new ContextObservabilityOptions()),
				null!));
	}

	[Fact]
	public void EnrichActivity_DoesNothing_WhenActivityIsNull()
	{
		// Arrange
		_enricher = CreateEnricher();

		// Act & Assert — should not throw
		_enricher.EnrichActivity(null, A.Fake<IMessageContext>());
	}

	[Fact]
	public void EnrichActivity_DoesNothing_WhenContextIsNull()
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

		// Act & Assert — should not throw
		_enricher.EnrichActivity(activity, null!);
	}

	[Fact]
	public void EnrichActivity_SetsStandardAttributes()
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

		// Act
		_enricher.EnrichActivity(activity, context);

		// Assert
		activity.GetTagItem("message.id").ShouldBe("msg-1");
		activity.GetTagItem("message.type").ShouldBe("OrderCreated");
		activity.GetTagItem("correlation.id").ShouldBe("corr-1");
		activity.GetTagItem("causation.id").ShouldBe("cause-1");
	}

	[Fact]
	public void EnrichActivity_SanitizesPII()
	{
		// Arrange
		A.CallTo(() => _fakeSanitizer.SanitizeTag("user.id", "user-123"))
			.Returns("REDACTED");
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
		A.CallTo(() => context.UserId).Returns("user-123");

		// Act
		_enricher.EnrichActivity(activity, context);

		// Assert
		activity.GetTagItem("user.id").ShouldBe("REDACTED");
	}

	[Fact]
	public void EnrichActivity_IncludesCustomItems_WhenEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Tracing.IncludeCustomItemsInTraces = true;
		options.Tracing.MaxCustomItemsInTraces = 5;
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
			["OrderId"] = "order-123",
			["Amount"] = 99.99,
		});

		// Act
		_enricher.EnrichActivity(activity, context);

		// Assert
		activity.GetTagItem("context.item.OrderId").ShouldNotBeNull();
	}

	[Fact]
	public void EnrichActivity_SkipsSensitiveItems()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Tracing.IncludeCustomItemsInTraces = true;
		options.Tracing.SensitiveFieldPatterns = ["(?i)password"];
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
			["password_hash"] = "secret123",
			["safe_data"] = "visible",
		});

		// Act
		_enricher.EnrichActivity(activity, context);

		// Assert
		activity.GetTagItem("context.item.password_hash").ShouldBeNull();
	}

	[Fact]
	public void CreateContextOperationSpan_ReturnsActivity()
	{
		// Arrange
		_enricher = CreateEnricher();
		using var listener = new ActivityListener
		{
			ShouldListenTo = s => s.Name.Contains("Enricher"),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		var context = CreateFakeContext();

		// Act
		using var activity = _enricher.CreateContextOperationSpan("ProcessMessage", context);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("context.operation").ShouldBe("ProcessMessage");
	}

	[Fact]
	public void LinkRelatedTrace_DoesNothing_WhenActivityIsNull()
	{
		_enricher = CreateEnricher();
		// Should not throw
		_enricher.LinkRelatedTrace(null, "corr-1");
	}

	[Fact]
	public void LinkRelatedTrace_DoesNothing_WhenCorrelationIdEmpty()
	{
		_enricher = CreateEnricher();
		using var source = new ActivitySource("test");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		using var activity = source.StartActivity("test-op");

		// Should not throw
		_enricher.LinkRelatedTrace(activity, "");
		_enricher.LinkRelatedTrace(activity, null!);
	}

	[Fact]
	public void LinkRelatedTrace_CreatesLink()
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
		_enricher.LinkRelatedTrace(activity, "corr-123", "follows-from");
	}

	[Fact]
	public void PropagateContextAsBaggage_ThrowsOnNullContext()
	{
		_enricher = CreateEnricher();
		Should.Throw<ArgumentNullException>(() =>
			_enricher.PropagateContextAsBaggage(null!, new Dictionary<string, string>()));
	}

	[Fact]
	public void PropagateContextAsBaggage_ThrowsOnNullCarrier()
	{
		_enricher = CreateEnricher();
		Should.Throw<ArgumentNullException>(() =>
			_enricher.PropagateContextAsBaggage(A.Fake<IMessageContext>(), null!));
	}

	[Fact]
	public void PropagateContextAsBaggage_AddsBaggageItems()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = CreateFakeContext();
		var carrier = new Dictionary<string, string>();

		// Act
		_enricher.PropagateContextAsBaggage(context, carrier);

		// Assert
		carrier.ShouldContainKey("correlation.id");
		carrier["correlation.id"].ShouldBe("corr-1");
		carrier.ShouldContainKey("message.type");
	}

	[Fact]
	public void PropagateContextAsBaggage_SanitizesPII()
	{
		// Arrange
		A.CallTo(() => _fakeSanitizer.SanitizeTag("user.id", "user-123"))
			.Returns("hash-user");
		_enricher = CreateEnricher();
		var context = CreateFakeContext();
		A.CallTo(() => context.UserId).Returns("user-123");
		var carrier = new Dictionary<string, string>();

		// Act
		_enricher.PropagateContextAsBaggage(context, carrier);

		// Assert
		carrier.ShouldContainKey("user.id");
		carrier["user.id"].ShouldBe("hash-user");
	}

	[Fact]
	public void ExtractContextFromBaggage_DoesNothing_WhenCarrierIsNull()
	{
		_enricher = CreateEnricher();
		// Should not throw
		_enricher.ExtractContextFromBaggage(null!, A.Fake<IMessageContext>());
	}

	[Fact]
	public void ExtractContextFromBaggage_DoesNothing_WhenContextIsNull()
	{
		_enricher = CreateEnricher();
		// Should not throw
		_enricher.ExtractContextFromBaggage(new Dictionary<string, string>(), null!);
	}

	[Fact]
	public void AddContextEvent_ThrowsOnNullEventName()
	{
		_enricher = CreateEnricher();
		Should.Throw<ArgumentNullException>(() =>
			_enricher.AddContextEvent(null!, A.Fake<IMessageContext>()));
	}

	[Fact]
	public void AddContextEvent_ThrowsOnNullContext()
	{
		_enricher = CreateEnricher();
		Should.Throw<ArgumentNullException>(() =>
			_enricher.AddContextEvent("test", null!));
	}

	[Fact]
	public void AddContextEvent_AddsEvent_WhenActivityExists()
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
		var attrs = new Dictionary<string, object> { ["custom"] = "value" };

		// Act
		_enricher.AddContextEvent("order.processed", context, attrs);

		// Assert
		activity.Events.ShouldContain(e => e.Name == "order.processed");
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		var enricher = CreateEnricher();
		enricher.Dispose();
		enricher.Dispose(); // Idempotent
		_enricher = null; // Prevent teardown
	}

	[Fact]
	public void EnrichActivity_LinksToParentTrace_WhenTraceParentSet()
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
		// Valid W3C trace parent format: version-traceId-spanId-flags
		A.CallTo(() => context.TraceParent)
			.Returns("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");

		// Act
		_enricher.EnrichActivity(activity, context);

		// Assert
		activity.GetTagItem("parent.trace_id").ShouldNotBeNull();
		activity.GetTagItem("parent.span_id").ShouldNotBeNull();
	}

	[Fact]
	public void EnrichActivity_HandlesInvalidTraceParent()
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
		A.CallTo(() => context.TraceParent).Returns("invalid-trace-parent");

		// Act — should not throw
		_enricher.EnrichActivity(activity, context);

		// Assert — no parent tags set
		activity.GetTagItem("parent.trace_id").ShouldBeNull();
	}

	private ContextTraceEnricher CreateEnricher(ContextObservabilityOptions? options = null)
	{
		return new ContextTraceEnricher(
			NullLogger<ContextTraceEnricher>.Instance,
			MsOptions.Create(options ?? new ContextObservabilityOptions()),
			_fakeSanitizer);
	}

	private static IMessageContext CreateFakeContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.CausationId).Returns("cause-1");
		A.CallTo(() => context.MessageType).Returns("OrderCreated");
		A.CallTo(() => context.UserId).Returns("user-1");
		A.CallTo(() => context.TenantId).Returns("tenant-1");
		A.CallTo(() => context.Source).Returns("test-source");
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}
}

#pragma warning restore IL2026, IL3050
