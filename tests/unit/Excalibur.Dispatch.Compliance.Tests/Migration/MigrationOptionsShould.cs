using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationOptionsShould
{
	[Fact]
	public void Have_default_target_version_of_v11()
	{
		var options = new MigrationOptions();

		options.TargetVersion.ShouldBe(EncryptionVersion.Version11);
	}

	[Fact]
	public void Enable_lazy_re_encryption_by_default()
	{
		var options = new MigrationOptions();

		options.EnableLazyReEncryption.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_max_concurrent_migrations_of_4()
	{
		var options = new MigrationOptions();

		options.MaxConcurrentMigrations.ShouldBe(4);
	}

	[Fact]
	public void Have_default_batch_size_of_100()
	{
		var options = new MigrationOptions();

		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void Track_progress_by_default()
	{
		var options = new MigrationOptions();

		options.TrackProgress.ShouldBeTrue();
	}

	[Fact]
	public void Not_fail_fast_by_default()
	{
		var options = new MigrationOptions();

		options.FailFast.ShouldBeFalse();
	}

	[Fact]
	public void Have_default_migration_timeout_of_30_seconds()
	{
		var options = new MigrationOptions();

		options.MigrationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Allow_setting_custom_values()
	{
		var options = new MigrationOptions
		{
			TargetVersion = EncryptionVersion.Version10,
			EnableLazyReEncryption = false,
			MaxConcurrentMigrations = 8,
			BatchSize = 500,
			TrackProgress = false,
			FailFast = true,
			MigrationTimeout = TimeSpan.FromMinutes(2)
		};

		options.TargetVersion.ShouldBe(EncryptionVersion.Version10);
		options.EnableLazyReEncryption.ShouldBeFalse();
		options.MaxConcurrentMigrations.ShouldBe(8);
		options.BatchSize.ShouldBe(500);
		options.TrackProgress.ShouldBeFalse();
		options.FailFast.ShouldBeTrue();
		options.MigrationTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}
}
