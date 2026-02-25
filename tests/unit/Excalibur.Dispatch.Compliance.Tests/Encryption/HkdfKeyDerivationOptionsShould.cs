using System.Security.Cryptography;

using Excalibur.Dispatch.Compliance.Encryption;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class HkdfKeyDerivationOptionsShould
{
	[Fact]
	public void Default_to_sha256_hash_algorithm()
	{
		var options = new HkdfKeyDerivationOptions();

		options.HashAlgorithm.ShouldBe(HashAlgorithmName.SHA256);
	}

	[Fact]
	public void Have_default_output_length_of_32_bytes()
	{
		var options = new HkdfKeyDerivationOptions();

		options.DefaultOutputLength.ShouldBe(32);
	}

	[Fact]
	public void Allow_setting_custom_hash_algorithm()
	{
		var options = new HkdfKeyDerivationOptions
		{
			HashAlgorithm = HashAlgorithmName.SHA512
		};

		options.HashAlgorithm.ShouldBe(HashAlgorithmName.SHA512);
	}

	[Fact]
	public void Allow_setting_custom_output_length()
	{
		var options = new HkdfKeyDerivationOptions
		{
			DefaultOutputLength = 64
		};

		options.DefaultOutputLength.ShouldBe(64);
	}
}
