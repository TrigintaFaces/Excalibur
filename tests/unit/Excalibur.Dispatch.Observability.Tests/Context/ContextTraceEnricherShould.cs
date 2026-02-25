// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextTraceEnricher"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextTraceEnricherShould : IDisposable
{
	private readonly ITelemetrySanitizer _fakeSanitizer = A.Fake<ITelemetrySanitizer>();
	private readonly ActivitySource _activitySource = new("Test.Enricher");
	private readonly ActivityListener _listener;
	private ContextTraceEnricher? _enricher;

	public ContextTraceEnricherShould()
	{
		// Set up sanitizer to pass through values
		A.CallTo(() => _fakeSanitizer.SanitizeTag(A<string>._, A<string?>._))
			.ReturnsLazily(call => call.Arguments[1] as string);

		_listener = new ActivityListener
		{
			ShouldListenTo = source => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_enricher?.Dispose();
		_listener.Dispose();
		_activitySource.Dispose();
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextTraceEnricher(null!,
				MsOptions.Create(new ContextObservabilityOptions()),
				_fakeSanitizer));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextTraceEnricher(NullLogger<ContextTraceEnricher>.Instance,
				null!,
				_fakeSanitizer));
	}

	[Fact]
	public void ThrowOnNullSanitizer()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextTraceEnricher(NullLogger<ContextTraceEnricher>.Instance,
				MsOptions.Create(new ContextObservabilityOptions()),
				null!));
	}

#pragma warning disable IL2026, IL3050
	[Fact]
	public void EnrichActivity_DoesNotThrow_WithNullActivity()
	{
		_enricher = CreateEnricher();
		_enricher.EnrichActivity(null, A.Fake<IMessageContext>());
	}

	[Fact]
	public void EnrichActivity_DoesNotThrow_WithNullContext()
	{
		_enricher = CreateEnricher();
		using var activity = _activitySource.StartActivity("test");
		_enricher.EnrichActivity(activity, null!);
	}

	[Fact]
	public void EnrichActivity_AddsStandardAttributes()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.MessageType).Returns("TestMessage");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		using var activity = _activitySource.StartActivity("test");

		// Act
		_enricher.EnrichActivity(activity, context);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("message.id").ShouldBe("msg-1");
		activity.GetTagItem("message.type").ShouldBe("TestMessage");
		activity.GetTagItem("correlation.id").ShouldBe("corr-1");
	}

	[Fact]
	public void CreateContextOperationSpan_ReturnsActivity()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		using var activity = _enricher.CreateContextOperationSpan("TestOp", context);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("context.operation").ShouldBe("TestOp");
	}
#pragma warning restore IL2026, IL3050

	[Fact]
	public void LinkRelatedTrace_DoesNotThrow_WithNullActivity()
	{
		_enricher = CreateEnricher();
		_enricher.LinkRelatedTrace(null, "corr-1");
	}

	[Fact]
	public void LinkRelatedTrace_DoesNotThrow_WithEmptyCorrelation()
	{
		_enricher = CreateEnricher();
		using var activity = _activitySource.StartActivity("test");
		_enricher.LinkRelatedTrace(activity, "");
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
	public void PropagateContextAsBaggage_PopulatesCarrier()
	{
		// Arrange
		_enricher = CreateEnricher();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.CausationId).Returns("cause-1");
		A.CallTo(() => context.MessageType).Returns("TestMessage");

		var carrier = new Dictionary<string, string>();

		// Act
		_enricher.PropagateContextAsBaggage(context, carrier);

		// Assert
		carrier.ShouldContainKey("correlation.id");
		carrier["correlation.id"].ShouldBe("corr-1");
		carrier.ShouldContainKey("causation.id");
		carrier.ShouldContainKey("message.type");
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
	public void ImplementIContextTraceEnricher()
	{
		_enricher = CreateEnricher();
		_enricher.ShouldBeAssignableTo<IContextTraceEnricher>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_enricher = CreateEnricher();
		_enricher.ShouldBeAssignableTo<IDisposable>();
	}

	private ContextTraceEnricher CreateEnricher()
	{
		return new ContextTraceEnricher(
			NullLogger<ContextTraceEnricher>.Instance,
			MsOptions.Create(new ContextObservabilityOptions()),
			_fakeSanitizer);
	}
}
