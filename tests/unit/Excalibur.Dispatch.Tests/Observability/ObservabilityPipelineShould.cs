// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Observability;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class ObservabilityPipelineShould : IDisposable
{
	private readonly DispatchTelemetryProvider _provider;

	public ObservabilityPipelineShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions
		{
			ServiceName = "test-service",
			ServiceVersion = "1.0.0",
			EnableTracing = true,
			EnableMetrics = true,
		});
		_provider = new DispatchTelemetryProvider(options);
	}

	public void Dispose() => _provider.Dispose();

	[Fact]
	public void CreateActivitySourceForCoreComponent()
	{
		// Act
		var activitySource = _provider.GetActivitySource(DispatchTelemetryConstants.ActivitySources.Core);

		// Assert
		activitySource.ShouldNotBeNull();
		activitySource.Name.ShouldBe(DispatchTelemetryConstants.ActivitySources.Core);
	}

	[Fact]
	public void CreateActivitySourceForPipelineComponent()
	{
		// Act
		var activitySource = _provider.GetActivitySource(DispatchTelemetryConstants.ActivitySources.Pipeline);

		// Assert
		activitySource.ShouldNotBeNull();
		activitySource.Name.ShouldBe(DispatchTelemetryConstants.ActivitySources.Pipeline);
	}

	[Fact]
	public void ReturnSameActivitySourceForSameComponent()
	{
		// Act
		var first = _provider.GetActivitySource(DispatchTelemetryConstants.ActivitySources.Core);
		var second = _provider.GetActivitySource(DispatchTelemetryConstants.ActivitySources.Core);

		// Assert -- same component should return cached instance
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void ReturnDifferentActivitySourcesForDifferentComponents()
	{
		// Act
		var core = _provider.GetActivitySource(DispatchTelemetryConstants.ActivitySources.Core);
		var pipeline = _provider.GetActivitySource(DispatchTelemetryConstants.ActivitySources.Pipeline);

		// Assert
		core.ShouldNotBeSameAs(pipeline);
		core.Name.ShouldNotBe(pipeline.Name);
	}

	[Fact]
	public void CreateMeterForCoreComponent()
	{
		// Act
		var meter = _provider.GetMeter(DispatchTelemetryConstants.Meters.Core);

		// Assert
		meter.ShouldNotBeNull();
		meter.Name.ShouldBe(DispatchTelemetryConstants.Meters.Core);
	}

	[Fact]
	public void ReturnSameMeterForSameComponent()
	{
		// Act
		var first = _provider.GetMeter(DispatchTelemetryConstants.Meters.Core);
		var second = _provider.GetMeter(DispatchTelemetryConstants.Meters.Core);

		// Assert
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void ThrowObjectDisposedExceptionAfterDispose()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions
		{
			ServiceName = "disposable-test",
			ServiceVersion = "1.0.0",
		});
		var provider = new DispatchTelemetryProvider(options);

		// Act
		provider.Dispose();

		// Assert
		Should.Throw<ObjectDisposedException>(
			() => provider.GetActivitySource("test"));
	}

	[Fact]
	public void ReturnNoOpActivitySourceWhenTracingDisabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions
		{
			ServiceName = "noop-test",
			ServiceVersion = "1.0.0",
			EnableTracing = false,
		});
		using var provider = new DispatchTelemetryProvider(options);

		// Act
		var activitySource = provider.GetActivitySource("test-component");

		// Assert -- should return a no-op source
		activitySource.ShouldNotBeNull();
		activitySource.Name.ShouldContain("NoOp");
	}

	[Fact]
	public void HaveConstantsForAllCoreActivitySources()
	{
		// Assert -- key ActivitySource constants must exist
		DispatchTelemetryConstants.ActivitySources.Core.ShouldNotBeNullOrEmpty();
		DispatchTelemetryConstants.ActivitySources.Pipeline.ShouldNotBeNullOrEmpty();

		// Core should follow the naming convention
		DispatchTelemetryConstants.ActivitySources.Core.ShouldStartWith("Excalibur.Dispatch");
		DispatchTelemetryConstants.ActivitySources.Pipeline.ShouldStartWith("Excalibur.Dispatch");
	}

	[Fact]
	public void HaveConstantsForAllCoreMeters()
	{
		// Assert -- key Meter constants must exist
		DispatchTelemetryConstants.Meters.Core.ShouldNotBeNullOrEmpty();
		DispatchTelemetryConstants.Meters.Core.ShouldStartWith("Excalibur.Dispatch");
	}

	[Fact]
	public void HaveActivitySourceAndMeterWithSameCoreName()
	{
		// Assert -- core ActivitySource and Meter share the same name
		DispatchTelemetryConstants.ActivitySources.Core
			.ShouldBe(DispatchTelemetryConstants.Meters.Core);
	}

	[Fact]
	public void ProduceActivityWhenListenerIsRegistered()
	{
		// Arrange
		Activity? capturedActivity = null;
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.StartsWith("Excalibur.Dispatch", StringComparison.Ordinal),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = activity => capturedActivity = activity,
		};
		ActivitySource.AddActivityListener(listener);

		var activitySource = _provider.GetActivitySource(DispatchTelemetryConstants.ActivitySources.Core);

		// Act
		using var activity = activitySource.StartActivity("test-operation");

		// Assert
		capturedActivity.ShouldNotBeNull();
		capturedActivity.OperationName.ShouldBe("test-operation");
	}
}
