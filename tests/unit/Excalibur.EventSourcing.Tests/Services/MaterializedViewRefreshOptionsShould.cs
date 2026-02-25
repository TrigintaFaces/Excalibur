using Excalibur.EventSourcing.Services;

namespace Excalibur.EventSourcing.Tests.Services;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MaterializedViewRefreshOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions();

		// Assert
		options.CatchUpOnStartup.ShouldBeFalse();
		options.RefreshInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.CronExpression.ShouldBeNull();
		options.BatchSize.ShouldBe(100);
		options.InitialRetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.MaxRetryCount.ShouldBe(5);
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingCatchUpOnStartup()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions { CatchUpOnStartup = true };

		// Assert
		options.CatchUpOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingRefreshInterval()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions
		{
			RefreshInterval = TimeSpan.FromMinutes(2)
		};

		// Assert
		options.RefreshInterval.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void AllowSettingRefreshIntervalToNull()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions { RefreshInterval = null };

		// Assert
		options.RefreshInterval.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingCronExpression()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions { CronExpression = "*/5 * * * *" };

		// Assert
		options.CronExpression.ShouldBe("*/5 * * * *");
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions { BatchSize = 500 };

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingRetryConfiguration()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions
		{
			InitialRetryDelay = TimeSpan.FromSeconds(5),
			MaxRetryDelay = TimeSpan.FromMinutes(10),
			MaxRetryCount = 10
		};

		// Assert
		options.InitialRetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.MaxRetryCount.ShouldBe(10);
	}

	[Fact]
	public void AllowDisablingService()
	{
		// Arrange & Act
		var options = new MaterializedViewRefreshOptions { Enabled = false };

		// Assert
		options.Enabled.ShouldBeFalse();
	}
}
