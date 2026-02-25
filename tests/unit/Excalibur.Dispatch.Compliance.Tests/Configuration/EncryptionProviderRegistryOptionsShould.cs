using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionProviderRegistryOptionsShould
{
	[Fact]
	public void Have_null_primary_provider_id_by_default()
	{
		var options = new EncryptionProviderRegistryOptions();

		options.PrimaryProviderId.ShouldBeNull();
	}

	[Fact]
	public void Have_empty_legacy_provider_ids_by_default()
	{
		var options = new EncryptionProviderRegistryOptions();

		options.LegacyProviderIds.ShouldNotBeNull();
		options.LegacyProviderIds.ShouldBeEmpty();
	}

	[Fact]
	public void Not_throw_on_decryption_provider_not_found_by_default()
	{
		var options = new EncryptionProviderRegistryOptions();

		options.ThrowOnDecryptionProviderNotFound.ShouldBeFalse();
	}

	[Fact]
	public void Validate_on_startup_by_default()
	{
		var options = new EncryptionProviderRegistryOptions();

		options.ValidateOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void Allow_setting_custom_values()
	{
		var options = new EncryptionProviderRegistryOptions
		{
			PrimaryProviderId = "aes-primary",
			LegacyProviderIds = ["old-aes", "deprecated-rsa"],
			ThrowOnDecryptionProviderNotFound = true,
			ValidateOnStartup = false
		};

		options.PrimaryProviderId.ShouldBe("aes-primary");
		options.LegacyProviderIds.Count.ShouldBe(2);
		options.LegacyProviderIds.ShouldContain("old-aes");
		options.LegacyProviderIds.ShouldContain("deprecated-rsa");
		options.ThrowOnDecryptionProviderNotFound.ShouldBeTrue();
		options.ValidateOnStartup.ShouldBeFalse();
	}
}
