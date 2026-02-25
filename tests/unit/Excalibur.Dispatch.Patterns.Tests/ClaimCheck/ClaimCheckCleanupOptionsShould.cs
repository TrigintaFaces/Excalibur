namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckCleanupOptionsShould
{
	[Fact]
	public void Have_correct_defaults()
	{
		// Arrange & Act
		var options = new ClaimCheckCleanupOptions();

		// Assert
		options.EnableCleanup.ShouldBeTrue();
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
		options.DefaultTtl.ShouldBe(TimeSpan.FromDays(7));
		options.CleanupBatchSize.ShouldBe(1000);
	}

	[Fact]
	public void Allow_disabling_cleanup()
	{
		// Arrange & Act
		var options = new ClaimCheckCleanupOptions { EnableCleanup = false };

		// Assert
		options.EnableCleanup.ShouldBeFalse();
	}

	[Fact]
	public void Allow_custom_cleanup_interval()
	{
		// Arrange & Act
		var options = new ClaimCheckCleanupOptions { CleanupInterval = TimeSpan.FromMinutes(30) };

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void Allow_custom_default_ttl()
	{
		// Arrange & Act
		var options = new ClaimCheckCleanupOptions { DefaultTtl = TimeSpan.FromDays(14) };

		// Assert
		options.DefaultTtl.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	public void Allow_custom_batch_size()
	{
		// Arrange & Act
		var options = new ClaimCheckCleanupOptions { CleanupBatchSize = 500 };

		// Assert
		options.CleanupBatchSize.ShouldBe(500);
	}
}
