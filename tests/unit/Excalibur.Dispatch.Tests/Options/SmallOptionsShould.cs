using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Validation.Context;
using Excalibur.Dispatch.Options.Channels;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Options.ErrorHandling;
using Excalibur.Dispatch.Options.Routing;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Options.Serialization;
using Excalibur.Dispatch.Options.Threading;
using Excalibur.Dispatch.Options.Validation;

namespace Excalibur.Dispatch.Tests.Options;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SmallOptionsShould
{
	// === Channels ===

	[Fact]
	public void DispatchChannelOptions_HaveDefaults()
	{
		var opts = new DispatchChannelOptions();

		opts.Mode.ShouldBe(ChannelMode.Unbounded);
		opts.Capacity.ShouldBeNull();
	}

	[Fact]
	public void BoundedDispatchChannelOptions_DefaultConstructor()
	{
		var opts = new BoundedDispatchChannelOptions();

		opts.Mode.ShouldBe(ChannelMode.Bounded);
	}

	[Fact]
	public void BoundedDispatchChannelOptions_CapacityConstructor()
	{
		var opts = new BoundedDispatchChannelOptions(500);

		opts.Mode.ShouldBe(ChannelMode.Bounded);
		opts.Capacity.ShouldBe(500);
	}

	[Fact]
	public void ChannelMessagePumpOptions_HaveDefaults()
	{
		var opts = new ChannelMessagePumpOptions();

		opts.Capacity.ShouldBe(100);
		opts.FullMode.ShouldBe(BoundedChannelFullMode.Wait);
	}

	[Fact]
	public void SpinWaitOptions_HaveDefaults()
	{
		var opts = new SpinWaitOptions();

		opts.SpinCount.ShouldBe(10);
		opts.DelayMilliseconds.ShouldBe(1);
	}

	// === Scheduling ===

	[Fact]
	public void CronScheduleOptions_HaveDefaults()
	{
		var opts = new CronScheduleOptions();

		opts.DefaultTimeZone.ShouldBe(TimeZoneInfo.Utc);
		opts.IncludeSeconds.ShouldBeFalse();
	}

	[Fact]
	public void SchedulerOptions_HaveDefaults()
	{
		var opts = new SchedulerOptions();

		opts.PollInterval.ShouldBe(TimeSpan.FromSeconds(30));
		opts.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.ExecuteImmediately);
	}

	// === Validation ===

	[Fact]
	public void ContextValidationOptions_HaveDefaults()
	{
		var opts = new ContextValidationOptions();

		opts.Mode.ShouldBe(ValidationMode.Lenient);
		opts.ValidateRequiredFields.ShouldBeTrue();
	}

	[Fact]
	public void VersioningOptions_HaveDefaults()
	{
		var opts = new VersioningOptions();

		opts.Enabled.ShouldBeTrue();
		opts.RequireContractVersion.ShouldBeTrue();
	}

	[Fact]
	public void VersioningOptions_AllowSettingProperties()
	{
		var opts = new VersioningOptions
		{
			Enabled = false,
			RequireContractVersion = false,
		};

		opts.Enabled.ShouldBeFalse();
		opts.RequireContractVersion.ShouldBeFalse();
	}

	// === Serialization ===

	[Fact]
	public void MessageSerializerOptions_HaveDefaults()
	{
		var opts = new MessageSerializerOptions();

		opts.SerializerMap.ShouldNotBeNull();
		opts.SerializerMap.ShouldBeEmpty();
	}

	[Fact]
	public void MessageSerializerOptions_AllowAddingSerializers()
	{
		var opts = new MessageSerializerOptions();
		opts.SerializerMap[1] = typeof(string);

		opts.SerializerMap.Count.ShouldBe(1);
		opts.SerializerMap[1].ShouldBe(typeof(string));
	}

	[Fact]
	public void DispatchJsonSerializerOptions_ProvidesDefaultInstance()
	{
		var defaults = DispatchJsonSerializerOptions.Default;

		defaults.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchJsonSerializerOptions_ProvidesWebInstance()
	{
		var web = DispatchJsonSerializerOptions.Web;

		web.ShouldNotBeNull();
	}

	// === CloudEvents ===

	[Fact]
	public void CloudEventBatchOptions_HaveDefaults()
	{
		var opts = new CloudEventBatchOptions();

		opts.MaxEvents.ShouldBe(100);
		opts.MaxBatchSizeBytes.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void CloudEventBatchOptions_AllowSettingProperties()
	{
		var opts = new CloudEventBatchOptions
		{
			MaxEvents = 50,
			MaxBatchSizeBytes = 512 * 1024,
		};

		opts.MaxEvents.ShouldBe(50);
		opts.MaxBatchSizeBytes.ShouldBe(512 * 1024);
	}

	// === Threading ===

	[Fact]
	public void ThreadingOptions_CanBeCreated()
	{
		var opts = new ThreadingOptions();

		opts.ShouldNotBeNull();
	}

	// === Routing ===

	[Fact]
	public void RoutingOptions_HaveDefaults()
	{
		var opts = new RoutingOptions();

		opts.RoutingPolicyPath.ShouldBeNull();
		opts.DefaultRemoteBusName.ShouldBeNull();
	}

	[Fact]
	public void RoutingOptions_AllowSettingProperties()
	{
		var opts = new RoutingOptions
		{
			RoutingPolicyPath = "/config/routing.json",
			DefaultRemoteBusName = "rabbitmq-bus",
		};

		opts.RoutingPolicyPath.ShouldBe("/config/routing.json");
		opts.DefaultRemoteBusName.ShouldBe("rabbitmq-bus");
	}

	// === ErrorHandling ===

	[Fact]
	public void PoisonMessageOptions_HaveDefaults()
	{
		var opts = new PoisonMessageOptions();

		opts.Enabled.ShouldBeTrue();
		opts.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void PoisonMessageOptions_AllowSettingProperties()
	{
		var opts = new PoisonMessageOptions
		{
			Enabled = false,
			MaxRetryAttempts = 5,
		};

		opts.Enabled.ShouldBeFalse();
		opts.MaxRetryAttempts.ShouldBe(5);
	}
}
