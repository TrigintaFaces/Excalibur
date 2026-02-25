using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionOptionsShould
{
	[Fact]
	public void Have_default_purpose_of_default()
	{
		var options = new EncryptionOptions();

		options.DefaultPurpose.ShouldBe("default");
	}

	[Fact]
	public void Not_require_fips_compliance_by_default()
	{
		var options = new EncryptionOptions();

		options.RequireFipsCompliance.ShouldBeFalse();
	}

	[Fact]
	public void Have_null_default_tenant_id()
	{
		var options = new EncryptionOptions();

		options.DefaultTenantId.ShouldBeNull();
	}

	[Fact]
	public void Include_timing_metadata_by_default()
	{
		var options = new EncryptionOptions();

		options.IncludeTimingMetadata.ShouldBeTrue();
	}

	[Fact]
	public void Have_null_encryption_age_warning_threshold_by_default()
	{
		var options = new EncryptionOptions();

		options.EncryptionAgeWarningThreshold.ShouldBeNull();
	}

	[Fact]
	public void Default_to_encrypt_and_decrypt_mode()
	{
		var options = new EncryptionOptions();

		options.Mode.ShouldBe(EncryptionMode.EncryptAndDecrypt);
	}

	[Fact]
	public void Have_lazy_migration_disabled_by_default()
	{
		var options = new EncryptionOptions();

		options.LazyMigrationEnabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_to_both_lazy_migration_mode()
	{
		var options = new EncryptionOptions();

		options.LazyMigrationMode.ShouldBe(LazyMigrationMode.Both);
	}

	[Fact]
	public void Allow_setting_custom_values()
	{
		var options = new EncryptionOptions
		{
			DefaultPurpose = "field-encryption",
			RequireFipsCompliance = true,
			DefaultTenantId = "tenant-1",
			IncludeTimingMetadata = false,
			EncryptionAgeWarningThreshold = TimeSpan.FromDays(30),
			Mode = EncryptionMode.DecryptOnlyWritePlaintext,
			LazyMigrationEnabled = true,
			LazyMigrationMode = LazyMigrationMode.OnWrite
		};

		options.DefaultPurpose.ShouldBe("field-encryption");
		options.RequireFipsCompliance.ShouldBeTrue();
		options.DefaultTenantId.ShouldBe("tenant-1");
		options.IncludeTimingMetadata.ShouldBeFalse();
		options.EncryptionAgeWarningThreshold.ShouldBe(TimeSpan.FromDays(30));
		options.Mode.ShouldBe(EncryptionMode.DecryptOnlyWritePlaintext);
		options.LazyMigrationEnabled.ShouldBeTrue();
		options.LazyMigrationMode.ShouldBe(LazyMigrationMode.OnWrite);
	}
}
