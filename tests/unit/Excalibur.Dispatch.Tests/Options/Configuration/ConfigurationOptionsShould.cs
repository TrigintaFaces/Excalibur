using Excalibur.Dispatch.Options.Configuration;

using ConfigInboxOptions = Excalibur.Dispatch.Options.Configuration.InboxConfigurationOptions;
using ConfigOutboxOptions = Excalibur.Dispatch.Options.Configuration.OutboxConfigurationOptions;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ConfigurationOptionsShould
{
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
	public void PerformanceOptions_HaveDefaults()
	{
		var opts = new PerformanceOptions();

		opts.EnableTypeMetadataCaching.ShouldBeTrue();
		opts.MessagePoolSize.ShouldBe(1000);
	}

}
