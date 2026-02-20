using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
	public void Validate_PassesWithDefaults()
	{
		var options = new DispatchTelemetryOptions();

		// Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_ThrowsOnNullServiceName()
	{
		var options = new DispatchTelemetryOptions { ServiceName = null! };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsOnEmptyServiceName()
	{
		var options = new DispatchTelemetryOptions { ServiceName = "" };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsOnNullServiceVersion()
	{
		var options = new DispatchTelemetryOptions { ServiceVersion = null! };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsOnZeroSlowOperationThreshold()
	{
		var options = new DispatchTelemetryOptions { SlowOperationThreshold = TimeSpan.Zero };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsOnNegativeSlowOperationThreshold()
	{
		var options = new DispatchTelemetryOptions { SlowOperationThreshold = TimeSpan.FromSeconds(-1) };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsOnZeroExportTimeout()
	{
		var options = new DispatchTelemetryOptions { ExportTimeout = TimeSpan.Zero };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsOnInvalidSamplingRatio_Negative()
	{
		var options = new DispatchTelemetryOptions { SamplingRatio = -0.1 };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsOnInvalidSamplingRatio_AboveOne()
	{
		var options = new DispatchTelemetryOptions { SamplingRatio = 1.1 };

		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void CreateProductionProfile()
	{
		var options = DispatchTelemetryOptions.CreateProductionProfile();

		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnablePipelineObservability.ShouldBeFalse();
		options.EnableHotPathMetrics.ShouldBeFalse();
		options.SamplingRatio.ShouldBe(0.01);
		options.MetricBatchSize.ShouldBe(1000);
	}

	[Fact]
	public void CreateDevelopmentProfile()
	{
		var options = DispatchTelemetryOptions.CreateDevelopmentProfile();

		options.EnableTracing.ShouldBeTrue();
		options.EnablePipelineObservability.ShouldBeTrue();
		options.EnableHotPathMetrics.ShouldBeTrue();
		options.SamplingRatio.ShouldBe(1.0);
	}

	[Fact]
	public void CreateThroughputProfile()
	{
		var options = DispatchTelemetryOptions.CreateThroughputProfile();

		options.EnableTracing.ShouldBeFalse();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableEnhancedStoreObservability.ShouldBeFalse();
		options.SamplingRatio.ShouldBe(0.001);
		options.MetricBatchSize.ShouldBe(2000);
	}

	[Fact]
	public void CopyTo_CopiesAllProperties()
	{
		var source = new DispatchTelemetryOptions
		{
			EnableTracing = false,
			EnableMetrics = false,
			EnableEnhancedStoreObservability = false,
			EnablePipelineObservability = false,
			EnableHotPathMetrics = true,
			ServiceName = "TestService",
			ServiceVersion = "2.0.0",
			SlowOperationThreshold = TimeSpan.FromSeconds(10),
			SamplingRatio = 0.5,
			MaxSpansPerTrace = 50,
			MetricBatchSize = 256,
			ExportTimeout = TimeSpan.FromSeconds(15),
		};
		source.GlobalTags["env"] = "test";

		var target = new DispatchTelemetryOptions();
		source.CopyTo(target);

		target.EnableTracing.ShouldBeFalse();
		target.EnableMetrics.ShouldBeFalse();
		target.EnableEnhancedStoreObservability.ShouldBeFalse();
		target.EnablePipelineObservability.ShouldBeFalse();
		target.EnableHotPathMetrics.ShouldBeTrue();
		target.ServiceName.ShouldBe("TestService");
		target.ServiceVersion.ShouldBe("2.0.0");
		target.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(10));
		target.SamplingRatio.ShouldBe(0.5);
		target.MaxSpansPerTrace.ShouldBe(50);
		target.MetricBatchSize.ShouldBe(256);
		target.ExportTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		target.GlobalTags["env"].ShouldBe("test");
	}

	[Fact]
	public void CopyTo_ThrowsOnNull()
	{
		var options = new DispatchTelemetryOptions();

		Should.Throw<ArgumentNullException>(() => options.CopyTo(null!));
	}

	[Fact]
	public void CopyTo_CreatesIndependentTagsCopy()
	{
		var source = new DispatchTelemetryOptions();
		source.GlobalTags["key"] = "value";

		var target = new DispatchTelemetryOptions();
		source.CopyTo(target);

		// Modifying source tags should not affect target
		source.GlobalTags["key"] = "modified";
		target.GlobalTags["key"].ShouldBe("value");
	}
}
