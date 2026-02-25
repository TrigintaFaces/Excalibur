using Excalibur.Dispatch.Options.Configuration;

using ConfigCachingOptions = Excalibur.Dispatch.Options.Configuration.CachingOptions;
using ConfigDeduplicationOptions = Excalibur.Dispatch.Options.Configuration.DeduplicationOptions;
using ConfigInboxOptions = Excalibur.Dispatch.Options.Configuration.InboxOptions;
using ConfigOutboxOptions = Excalibur.Dispatch.Options.Configuration.OutboxOptions;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfigurationOptionsShould
{
	[Fact]
	public void CachingOptions_HaveDefaults()
	{
		var opts = new ConfigCachingOptions();

		opts.Enabled.ShouldBeFalse();
		opts.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void CachingOptions_AllowSettingProperties()
	{
		var opts = new ConfigCachingOptions
		{
			Enabled = true,
			DefaultExpiration = TimeSpan.FromMinutes(30),
		};

		opts.Enabled.ShouldBeTrue();
		opts.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void ConsumerOptions_HaveDefaults()
	{
		var opts = new ConsumerOptions();

		opts.Dedupe.ShouldNotBeNull();
		opts.AckAfterHandle.ShouldBeTrue();
		opts.MaxConcurrentMessages.ShouldBe(10);
	}

	[Fact]
	public void ConsumerOptions_AllowSettingProperties()
	{
		var opts = new ConsumerOptions
		{
			AckAfterHandle = false,
			MaxConcurrentMessages = 50,
		};

		opts.AckAfterHandle.ShouldBeFalse();
		opts.MaxConcurrentMessages.ShouldBe(50);
	}

	[Fact]
	public void DeduplicationOptions_HaveDefaults()
	{
		var opts = new ConfigDeduplicationOptions();

		opts.Enabled.ShouldBeFalse();
		opts.ExpiryHours.ShouldBe(24);
		opts.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void DispatchOptions_HaveDefaults()
	{
		var opts = new DispatchOptions();

		opts.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		opts.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
		opts.UseLightMode.ShouldBeFalse();
	}

	[Fact]
	public void DispatchOptions_AllowSettingProperties()
	{
		var opts = new DispatchOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(60),
			MaxConcurrency = 16,
			UseLightMode = true,
		};

		opts.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		opts.MaxConcurrency.ShouldBe(16);
		opts.UseLightMode.ShouldBeTrue();
	}

	[Fact]
	public void InboxOptions_HaveDefaults()
	{
		var opts = new ConfigInboxOptions();

		opts.Enabled.ShouldBeFalse();
		opts.DeduplicationExpiryHours.ShouldBe(24);
		opts.AckAfterHandle.ShouldBeTrue();
		opts.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void ObservabilityOptions_HaveDefaults()
	{
		var opts = new ObservabilityOptions();

		opts.Enabled.ShouldBeTrue();
		opts.EnableTracing.ShouldBeTrue();
		opts.EnableMetrics.ShouldBeTrue();
		opts.EnableContextFlow.ShouldBeTrue();
	}

	[Fact]
	public void ObservabilityOptions_AllowSettingProperties()
	{
		var opts = new ObservabilityOptions
		{
			Enabled = false,
			EnableTracing = false,
			EnableMetrics = false,
			EnableContextFlow = false,
		};

		opts.Enabled.ShouldBeFalse();
		opts.EnableTracing.ShouldBeFalse();
		opts.EnableMetrics.ShouldBeFalse();
		opts.EnableContextFlow.ShouldBeFalse();
	}

	[Fact]
	public void OutboxOptions_HaveDefaults()
	{
		var opts = new ConfigOutboxOptions();

		opts.Enabled.ShouldBeTrue();
		opts.BatchSize.ShouldBe(100);
		opts.PublishIntervalMs.ShouldBe(1000);
		opts.MaxRetries.ShouldBe(3);
	}

	[Fact]
	public void OutboxOptions_AllowSettingProperties()
	{
		var opts = new ConfigOutboxOptions
		{
			Enabled = false,
			BatchSize = 500,
			PublishIntervalMs = 2000,
			MaxRetries = 5,
		};

		opts.Enabled.ShouldBeFalse();
		opts.BatchSize.ShouldBe(500);
		opts.PublishIntervalMs.ShouldBe(2000);
		opts.MaxRetries.ShouldBe(5);
	}

	[Fact]
	public void PerformanceOptions_HaveDefaults()
	{
		var opts = new PerformanceOptions();

		opts.EnableCacheMiddleware.ShouldBeTrue();
		opts.EnableTypeMetadataCaching.ShouldBeTrue();
		opts.MessagePoolSize.ShouldBe(1000);
	}

	[Fact]
	public void ResilienceOptions_HaveDefaults()
	{
		var opts = new ResilienceOptions();

		opts.DefaultRetryCount.ShouldBe(3);
		opts.EnableCircuitBreaker.ShouldBeFalse();
		opts.EnableTimeout.ShouldBeFalse();
	}

	[Fact]
	public void SecurityOptions_HaveDefaults()
	{
		var opts = new SecurityOptions();

		opts.EnableEncryption.ShouldBeFalse();
		opts.EnableSigning.ShouldBeFalse();
		opts.EnableRateLimiting.ShouldBeFalse();
		opts.EnableValidation.ShouldBeTrue();
	}
}
