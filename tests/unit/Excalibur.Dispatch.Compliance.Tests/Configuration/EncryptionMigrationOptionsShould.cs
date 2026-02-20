using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionMigrationOptionsShould
{
	[Fact]
	public void Have_default_batch_size_of_100()
	{
		var options = new EncryptionMigrationOptions();

		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void Have_default_max_parallelism_of_4()
	{
		var options = new EncryptionMigrationOptions();

		options.MaxDegreeOfParallelism.ShouldBe(4);
	}

	[Fact]
	public void Not_continue_on_error_by_default()
	{
		var options = new EncryptionMigrationOptions();

		options.ContinueOnError.ShouldBeFalse();
	}

	[Fact]
	public void Have_zero_delay_between_batches_by_default()
	{
		var options = new EncryptionMigrationOptions();

		options.DelayBetweenBatches.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void Have_null_source_provider_id_by_default()
	{
		var options = new EncryptionMigrationOptions();

		options.SourceProviderId.ShouldBeNull();
	}

	[Fact]
	public void Have_null_target_provider_id_by_default()
	{
		var options = new EncryptionMigrationOptions();

		options.TargetProviderId.ShouldBeNull();
	}

	[Fact]
	public void Verify_before_re_encrypt_by_default()
	{
		var options = new EncryptionMigrationOptions();

		options.VerifyBeforeReEncrypt.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_operation_timeout_of_30_seconds()
	{
		var options = new EncryptionMigrationOptions();

		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Allow_setting_custom_values()
	{
		var options = new EncryptionMigrationOptions
		{
			BatchSize = 500,
			MaxDegreeOfParallelism = 8,
			ContinueOnError = true,
			DelayBetweenBatches = TimeSpan.FromMilliseconds(100),
			SourceProviderId = "old-provider",
			TargetProviderId = "new-provider",
			VerifyBeforeReEncrypt = false,
			OperationTimeout = TimeSpan.FromMinutes(2)
		};

		options.BatchSize.ShouldBe(500);
		options.MaxDegreeOfParallelism.ShouldBe(8);
		options.ContinueOnError.ShouldBeTrue();
		options.DelayBetweenBatches.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.SourceProviderId.ShouldBe("old-provider");
		options.TargetProviderId.ShouldBe("new-provider");
		options.VerifyBeforeReEncrypt.ShouldBeFalse();
		options.OperationTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}
}
