using Excalibur.Dispatch.Options.Pooling;

namespace Excalibur.Dispatch.Tests.Options.Pooling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PoolingOptionsShould
{
	[Fact]
	public void BufferPoolOptions_HaveDefaults()
	{
		var opts = new BufferPoolOptions();

		opts.Enabled.ShouldBeTrue();
		opts.SizeBuckets.ShouldNotBeNull();
	}

	[Fact]
	public void GlobalPoolOptions_HaveDefaults()
	{
		var opts = new GlobalPoolOptions();

		opts.EnableTelemetry.ShouldBeFalse();
		opts.EnableDetailedMetrics.ShouldBeFalse();
		opts.EnableDiagnostics.ShouldBeTrue();
	}

	[Fact]
	public void MessagePoolOptions_HaveDefaults()
	{
		var opts = new MessagePoolOptions();

		opts.Enabled.ShouldBeTrue();
		opts.MaxPoolSizePerType.ShouldBe(Environment.ProcessorCount * 8);
		opts.AggressivePooling.ShouldBeTrue();
	}

	[Fact]
	public void PoolOptions_HaveDefaults()
	{
		var opts = new PoolOptions();

		opts.BufferPool.ShouldNotBeNull();
		opts.MessagePool.ShouldNotBeNull();
	}

	[Fact]
	public void SizeBucketOptions_HaveDefaults()
	{
		var opts = new SizeBucketOptions();

		opts.TinySize.ShouldBe(64);
		opts.SmallSize.ShouldBe(256);
	}

	[Fact]
	public void TypePoolOptions_HaveDefaults()
	{
		var opts = new TypePoolOptions();

		opts.MaxPoolSize.ShouldBe(0);
		opts.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void TypePoolOptions_AllowSettingProperties()
	{
		var opts = new TypePoolOptions
		{
			MaxPoolSize = 500,
			Enabled = false,
		};

		opts.MaxPoolSize.ShouldBe(500);
		opts.Enabled.ShouldBeFalse();
	}
}
