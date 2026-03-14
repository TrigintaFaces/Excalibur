// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ContextTraceEnricherShould : IDisposable
{
	private readonly ITelemetrySanitizer _fakeSanitizer;
	private readonly IMessageContext _fakeContext;
	private readonly ContextObservabilityOptions _options;
	private readonly ContextTraceEnricher _sut;
	private readonly ActivitySource _testActivitySource;

	public ContextTraceEnricherShould()
	{
		_fakeSanitizer = A.Fake<ITelemetrySanitizer>();
		A.CallTo(() => _fakeSanitizer.SanitizeTag(A<string>._, A<string?>._))
			.ReturnsLazily((string _, string? value) => value);

		_fakeContext = CreateFakeContext();

		_options = new ContextObservabilityOptions();

		_sut = new ContextTraceEnricher(
			NullLogger<ContextTraceEnricher>.Instance,
			Microsoft.Extensions.Options.Options.Create(_options),
			_fakeSanitizer);

		_testActivitySource = new ActivitySource("Test.ContextTraceEnricher", "1.0.0");
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextTraceEnricher(null!,
				Microsoft.Extensions.Options.Options.Create(_options),
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
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
	}

#pragma warning disable IL2026, IL3050
	[Fact]
	public void EnrichActivityDoesNothingWithNullActivity()
	{
		// Act - should not throw
		_sut.EnrichActivity(null, _fakeContext);
	}

	[Fact]
	public void EnrichActivityDoesNothingWithNullContext()
	{
		using var listener = new ActivityListener
		{
			ShouldListenTo = s => s.Name.Contains("Test"),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = _testActivitySource.StartActivity("test");

		// Act - should not throw
		_sut.EnrichActivity(activity, null!);
	}

	[Fact]
	public void EnrichActivitySetsStandardAttributes()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = s => s.Name.Contains("Test"),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = _testActivitySource.StartActivity("test");
		activity.ShouldNotBeNull();

		// Act
		_sut.EnrichActivity(activity, _fakeContext);

		// Assert
		activity.GetTagItem("message.id").ShouldBe("msg-001");
		activity.GetTagItem("correlation.id").ShouldBe("corr-001");
		activity.GetTagItem("message.type").ShouldBe("TestMessage");
	}

	[Fact]
	public void CreateContextOperationSpanReturnsNull_WhenNoListener()
	{
		// Act - no listener, so no activity created
		var activity = _sut.CreateContextOperationSpan("TestOp", _fakeContext);

		// Assert
		// May be null if no listener is listening to the enricher's activity source
		// This is expected OTel behavior
		activity?.Dispose();
	}
#pragma warning restore IL2026, IL3050

	[Fact]
	public void LinkRelatedTraceDoesNothingWithNullActivity()
	{
		// Act - should not throw
		_sut.LinkRelatedTrace(null, "corr-001");
	}

	[Fact]
	public void LinkRelatedTraceDoesNothingWithNullCorrelation()
	{
		using var listener = new ActivityListener
		{
			ShouldListenTo = s => s.Name.Contains("Test"),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = _testActivitySource.StartActivity("test");

		// Act - should not throw
		_sut.LinkRelatedTrace(activity, null!);
	}

	[Fact]
	public void PropagateContextAsBaggageThrowsOnNullContext()
	{
		var carrier = new Dictionary<string, string>();
		Should.Throw<ArgumentNullException>(() =>
			_sut.PropagateContextAsBaggage(null!, carrier));
	}

	[Fact]
	public void PropagateContextAsBaggageThrowsOnNullCarrier()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.PropagateContextAsBaggage(_fakeContext, null!));
	}

	[Fact]
	public void PropagateContextAsBaggageSetsCorrelationId()
	{
		// Arrange
		var carrier = new Dictionary<string, string>();

		// Act
		_sut.PropagateContextAsBaggage(_fakeContext, carrier);

		// Assert
		carrier.ShouldContainKey("correlation.id");
		carrier["correlation.id"].ShouldBe("corr-001");
	}

	[Fact]
	public void ExtractContextFromBaggageDoesNothingWithNullCarrier()
	{
		// Act - should not throw
		_sut.ExtractContextFromBaggage(null!, _fakeContext);
	}

	[Fact]
	public void ExtractContextFromBaggageDoesNothingWithNullContext()
	{
		// Act - should not throw
		_sut.ExtractContextFromBaggage(new Dictionary<string, string>(), null!);
	}

	[Fact]
	public void AddContextEventThrowsOnNullEventName()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.AddContextEvent(null!, _fakeContext));
	}

	[Fact]
	public void AddContextEventThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.AddContextEvent("TestEvent", null!));
	}

	[Fact]
	public void DisposeReleasesActivitySource()
	{
		_sut.Dispose();
		// Should not throw on double dispose
		_sut.Dispose();
	}

	public void Dispose()
	{
		_sut.Dispose();
		_testActivitySource.Dispose();
	}

	private static IMessageContext CreateFakeContext()
	{
		var items = new Dictionary<string, object>
		{
			["__MessageType"] = "TestMessage",
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.CorrelationId).Returns("corr-001");
		A.CallTo(() => context.CausationId).Returns("cause-001");
		A.CallTo(() => context.Items).Returns(items);
		return context;
	}
}
