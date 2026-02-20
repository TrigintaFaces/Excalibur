using Excalibur.EventSourcing.Views;

namespace Excalibur.EventSourcing.Tests.Views;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MaterializedViewOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new MaterializedViewOptions();

		// Assert
		options.CatchUpOnStartup.ShouldBeFalse();
		options.BatchSize.ShouldBe(100);
		options.BatchDelay.ShouldBe(TimeSpan.FromMilliseconds(10));
	}

	[Fact]
	public void AllowSettingCatchUpOnStartup()
	{
		// Arrange & Act
		var options = new MaterializedViewOptions { CatchUpOnStartup = true };

		// Assert
		options.CatchUpOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		// Arrange & Act
		var options = new MaterializedViewOptions { BatchSize = 500 };

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingBatchDelay()
	{
		// Arrange & Act
		var options = new MaterializedViewOptions { BatchDelay = TimeSpan.FromSeconds(1) };

		// Assert
		options.BatchDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}
}
