using Excalibur.Saga.Snapshots;

namespace Excalibur.Saga.Tests.Snapshots;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaSnapshotOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new SagaSnapshotOptions();

		// Assert
		options.SnapshotInterval.ShouldBe(10);
		options.EnableAutomaticSnapshots.ShouldBeTrue();
		options.MaxRetainedSnapshots.ShouldBe(3);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void AllowSettingSnapshotInterval()
	{
		// Arrange & Act
		var options = new SagaSnapshotOptions { SnapshotInterval = 50 };

		// Assert
		options.SnapshotInterval.ShouldBe(50);
	}

	[Fact]
	public void AllowDisablingAutomaticSnapshots()
	{
		// Arrange & Act
		var options = new SagaSnapshotOptions { EnableAutomaticSnapshots = false };

		// Assert
		options.EnableAutomaticSnapshots.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingMaxRetainedSnapshots()
	{
		// Arrange & Act
		var options = new SagaSnapshotOptions { MaxRetainedSnapshots = 10 };

		// Assert
		options.MaxRetainedSnapshots.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingRetentionPeriod()
	{
		// Arrange & Act
		var options = new SagaSnapshotOptions { RetentionPeriod = TimeSpan.FromDays(30) };

		// Assert
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}
}
