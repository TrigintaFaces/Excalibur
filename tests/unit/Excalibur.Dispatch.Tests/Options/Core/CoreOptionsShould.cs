using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CoreOptionsShould
{
	[Fact]
	public void CompressionOptions_HaveDefaults()
	{
		var opts = new CompressionOptions();

		opts.Enabled.ShouldBeFalse();
		opts.CompressionType.ShouldBe(CompressionType.Gzip);
		opts.CompressionLevel.ShouldBe(6);
		opts.MinimumSizeThreshold.ShouldBe(1024);
	}

	[Fact]
	public void CompressionOptions_AllowSettingProperties()
	{
		var opts = new CompressionOptions
		{
			Enabled = true,
			CompressionType = CompressionType.Brotli,
			CompressionLevel = 9,
			MinimumSizeThreshold = 512,
		};

		opts.Enabled.ShouldBeTrue();
		opts.CompressionType.ShouldBe(CompressionType.Brotli);
		opts.CompressionLevel.ShouldBe(9);
		opts.MinimumSizeThreshold.ShouldBe(512);
	}

	[Fact]
	public void HealthCheckOptions_HaveDefaults()
	{
		var opts = new HealthCheckOptions();

		opts.Enabled.ShouldBeFalse();
		opts.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
		opts.Interval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void InMemoryBusOptions_HaveDefaults()
	{
		var opts = new InMemoryBusOptions();

		opts.MaxQueueLength.ShouldBe(1000);
		opts.PreserveOrder.ShouldBeTrue();
		opts.ProcessingDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void InMemoryBusOptions_AllowSettingProperties()
	{
		var opts = new InMemoryBusOptions
		{
			MaxQueueLength = 500,
			PreserveOrder = false,
			ProcessingDelay = TimeSpan.FromMilliseconds(50),
		};

		opts.MaxQueueLength.ShouldBe(500);
		opts.PreserveOrder.ShouldBeFalse();
		opts.ProcessingDelay.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void MetricsOptions_HaveDefaults()
	{
		var opts = new MetricsOptions();

		opts.Enabled.ShouldBeFalse();
		opts.ExportInterval.ShouldBe(TimeSpan.FromSeconds(30));
		opts.CustomTags.ShouldBeEmpty();
	}

	[Fact]
	public void MetricsOptions_AllowSettingProperties()
	{
		var opts = new MetricsOptions
		{
			Enabled = true,
			ExportInterval = TimeSpan.FromSeconds(10),
		};
		opts.CustomTags["env"] = "test";

		opts.Enabled.ShouldBeTrue();
		opts.ExportInterval.ShouldBe(TimeSpan.FromSeconds(10));
		opts.CustomTags["env"].ShouldBe("test");
	}

	[Fact]
	public void TracingOptions_HaveDefaults()
	{
		var opts = new TracingOptions();

		opts.Enabled.ShouldBeFalse();
		opts.SamplingRatio.ShouldBe(1.0);
		opts.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void TracingOptions_AllowSettingProperties()
	{
		var opts = new TracingOptions
		{
			Enabled = true,
			SamplingRatio = 0.5,
			IncludeSensitiveData = true,
		};

		opts.Enabled.ShouldBeTrue();
		opts.SamplingRatio.ShouldBe(0.5);
		opts.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void MessageBusHealthCheckOptions_HaveDefaults()
	{
		var opts = new MessageBusHealthCheckOptions();

		opts.ShouldNotBeNull();
	}

}
