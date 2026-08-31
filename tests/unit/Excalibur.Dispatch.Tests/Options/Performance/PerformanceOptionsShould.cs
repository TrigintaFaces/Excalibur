using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Options.Performance;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PerformanceOptionsShould
{

	[Fact]
	public void MicroBatchOptions_HaveDefaults()
	{
		var opts = new MicroBatchOptions();

		opts.MaxBatchSize.ShouldBe(100);
		opts.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void MicroBatchOptions_AllowSettingProperties()
	{
		var opts = new MicroBatchOptions
		{
			MaxBatchSize = 50,
			MaxBatchDelay = TimeSpan.FromMilliseconds(200),
		};

		opts.MaxBatchSize.ShouldBe(50);
		opts.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
	}

}
