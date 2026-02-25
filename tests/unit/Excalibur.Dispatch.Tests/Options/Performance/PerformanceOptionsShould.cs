using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Options.Performance;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PerformanceOptionsShould
{
	[Fact]
	public void LeakTrackingOptions_HaveDefaults()
	{
		var opts = new LeakTrackingOptions();

		opts.MaximumRetained.ShouldBe(Environment.ProcessorCount * 2);
		opts.MinimumRetained.ShouldBe(Environment.ProcessorCount);
	}

	[Fact]
	public void LeakTrackingOptions_AllowSettingProperties()
	{
		var opts = new LeakTrackingOptions
		{
			MaximumRetained = 32,
			MinimumRetained = 4,
		};

		opts.MaximumRetained.ShouldBe(32);
		opts.MinimumRetained.ShouldBe(4);
	}

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

	[Fact]
	public void ShardedExecutorOptions_HaveDefaults()
	{
		var opts = new ShardedExecutorOptions();

		opts.ShardCount.ShouldBe(0);
		opts.MaxQueueDepth.ShouldBe(1000);
	}

	[Fact]
	public void ShardedExecutorOptions_AllowSettingProperties()
	{
		var opts = new ShardedExecutorOptions
		{
			ShardCount = 8,
			MaxQueueDepth = 500,
		};

		opts.ShardCount.ShouldBe(8);
		opts.MaxQueueDepth.ShouldBe(500);
	}

	[Fact]
	public void TunedArrayPoolOptions_HaveDefaults()
	{
		var opts = new TunedArrayPoolOptions();

		opts.PreWarmPools.ShouldBeTrue();
		opts.ClearOnReturn.ShouldBeFalse();
	}

	[Fact]
	public void TunedArrayPoolOptions_AllowSettingProperties()
	{
		var opts = new TunedArrayPoolOptions
		{
			PreWarmPools = false,
			ClearOnReturn = true,
		};

		opts.PreWarmPools.ShouldBeFalse();
		opts.ClearOnReturn.ShouldBeTrue();
	}

	[Fact]
	public void ZeroAllocOptions_HaveDefaults()
	{
		var opts = new ZeroAllocOptions();

		opts.ContextPoolSize.ShouldBe(1024);
		opts.MaxBufferSize.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void ZeroAllocOptions_AllowSettingProperties()
	{
		var opts = new ZeroAllocOptions
		{
			ContextPoolSize = 512,
			MaxBufferSize = 2048,
		};

		opts.ContextPoolSize.ShouldBe(512);
		opts.MaxBufferSize.ShouldBe(2048);
	}
}
