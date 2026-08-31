using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Options.Transport;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class TransportOptionsShould
{


	[Fact]
	public void CronTimerOptions_HaveDefaults()
	{
		var opts = new CronTimerOptions();

		opts.TimeZone.ShouldBe(TimeZoneInfo.Utc);
		opts.RunOnStartup.ShouldBeFalse();
		opts.PreventOverlap.ShouldBeTrue();
	}

	[Fact]
	public void CronTimerOptions_AllowSettingProperties()
	{
		var opts = new CronTimerOptions
		{
			TimeZone = TimeZoneInfo.Local,
			RunOnStartup = true,
			PreventOverlap = false,
		};

		opts.TimeZone.ShouldBe(TimeZoneInfo.Local);
		opts.RunOnStartup.ShouldBeTrue();
		opts.PreventOverlap.ShouldBeFalse();
	}

}
