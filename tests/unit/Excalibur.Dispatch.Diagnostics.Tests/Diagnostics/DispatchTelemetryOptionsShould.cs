// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

public sealed class DispatchTelemetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var options = new DispatchTelemetryOptions();

		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableEnhancedStoreObservability.ShouldBeTrue();
		options.EnablePipelineObservability.ShouldBeTrue();
		options.EnableHotPathMetrics.ShouldBeFalse();
		options.ServiceName.ShouldBe("Excalibur.Dispatch");
		options.ServiceVersion.ShouldBe("1.0.0");
		options.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(2));
		options.SamplingRatio.ShouldBe(0.1);
		options.MaxSpansPerTrace.ShouldBe(100);
		options.MetricBatchSize.ShouldBe(512);
		options.ExportTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.GlobalTags.ShouldNotBeNull();
		options.GlobalTags.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		var options = new DispatchTelemetryOptions();

		Should.NotThrow(() => options.Validate());
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void ThrowOnInvalidServiceName(string? serviceName)
	{
		var options = new DispatchTelemetryOptions { ServiceName = serviceName! };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void ThrowOnInvalidServiceVersion(string? serviceVersion)
	{
		var options = new DispatchTelemetryOptions { ServiceVersion = serviceVersion! };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void ThrowOnNegativeSlowOperationThreshold()
	{
		var options = new DispatchTelemetryOptions { SlowOperationThreshold = TimeSpan.FromSeconds(-1) };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void ThrowOnZeroSlowOperationThreshold()
	{
		var options = new DispatchTelemetryOptions { SlowOperationThreshold = TimeSpan.Zero };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void ThrowOnNegativeExportTimeout()
	{
		var options = new DispatchTelemetryOptions { ExportTimeout = TimeSpan.FromSeconds(-1) };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	[InlineData(2.0)]
	public void ThrowOnInvalidSamplingRatio(double ratio)
	{
		var options = new DispatchTelemetryOptions { SamplingRatio = ratio };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(0.5)]
	[InlineData(1.0)]
	public void AcceptValidSamplingRatio(double ratio)
	{
		var options = new DispatchTelemetryOptions { SamplingRatio = ratio };

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void CreateProductionProfileWithCorrectSettings()
	{
		var options = DispatchTelemetryOptions.CreateProductionProfile();

		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnablePipelineObservability.ShouldBeFalse();
		options.EnableHotPathMetrics.ShouldBeFalse();
		options.SamplingRatio.ShouldBe(0.01);
	}

	[Fact]
	public void CreateDevelopmentProfileWithCorrectSettings()
	{
		var options = DispatchTelemetryOptions.CreateDevelopmentProfile();

		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnablePipelineObservability.ShouldBeTrue();
		options.EnableHotPathMetrics.ShouldBeTrue();
		options.SamplingRatio.ShouldBe(1.0);
	}

	[Fact]
	public void CreateThroughputProfileWithCorrectSettings()
	{
		var options = DispatchTelemetryOptions.CreateThroughputProfile();

		options.EnableTracing.ShouldBeFalse();
		options.EnableMetrics.ShouldBeTrue();
		options.EnablePipelineObservability.ShouldBeFalse();
		options.EnableHotPathMetrics.ShouldBeFalse();
		options.SamplingRatio.ShouldBe(0.001);
	}

	[Fact]
	public void CopyAllPropertiesToTarget()
	{
		var source = new DispatchTelemetryOptions
		{
			EnableTracing = false,
			EnableMetrics = false,
			EnableEnhancedStoreObservability = false,
			EnablePipelineObservability = false,
			EnableHotPathMetrics = true,
			ServiceName = "TestService",
			ServiceVersion = "3.0.0",
			SlowOperationThreshold = TimeSpan.FromSeconds(5),
			SamplingRatio = 0.5,
			MaxSpansPerTrace = 200,
			MetricBatchSize = 1000,
			ExportTimeout = TimeSpan.FromSeconds(60),
			GlobalTags = new Dictionary<string, string> { ["env"] = "test" }
		};
		var target = new DispatchTelemetryOptions();

		source.CopyTo(target);

		target.EnableTracing.ShouldBe(source.EnableTracing);
		target.EnableMetrics.ShouldBe(source.EnableMetrics);
		target.EnableEnhancedStoreObservability.ShouldBe(source.EnableEnhancedStoreObservability);
		target.EnablePipelineObservability.ShouldBe(source.EnablePipelineObservability);
		target.EnableHotPathMetrics.ShouldBe(source.EnableHotPathMetrics);
		target.ServiceName.ShouldBe(source.ServiceName);
		target.ServiceVersion.ShouldBe(source.ServiceVersion);
		target.SlowOperationThreshold.ShouldBe(source.SlowOperationThreshold);
		target.SamplingRatio.ShouldBe(source.SamplingRatio);
		target.MaxSpansPerTrace.ShouldBe(source.MaxSpansPerTrace);
		target.MetricBatchSize.ShouldBe(source.MetricBatchSize);
		target.ExportTimeout.ShouldBe(source.ExportTimeout);
		target.GlobalTags["env"].ShouldBe("test");
	}

	[Fact]
	public void CopyGlobalTagsAsIndependentCopy()
	{
		var source = new DispatchTelemetryOptions
		{
			GlobalTags = new Dictionary<string, string> { ["key"] = "value" }
		};
		var target = new DispatchTelemetryOptions();

		source.CopyTo(target);
		source.GlobalTags["key"] = "changed";

		target.GlobalTags["key"].ShouldBe("value");
	}

	[Fact]
	public void ThrowOnCopyToNull()
	{
		var options = new DispatchTelemetryOptions();

		Should.Throw<ArgumentNullException>(() => options.CopyTo(null!));
	}
}
