using System.Security.Cryptography;

using Excalibur.Dispatch.Compliance.Encryption;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class HkdfKeyDeriverShould
{
	private readonly HkdfKeyDeriver _sut;

	public HkdfKeyDeriverShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new HkdfKeyDerivationOptions());
		_sut = new HkdfKeyDeriver(options);
	}

	[Fact]
	public void Derive_key_with_purpose()
	{
		// Arrange
		var masterKey = RandomNumberGenerator.GetBytes(32);

		// Act
		var derived = _sut.DeriveKey(masterKey, "field-encryption");

		// Assert
		derived.ShouldNotBeNull();
		derived.Length.ShouldBe(32); // Default output length
	}

	[Fact]
	public void Derive_key_with_purpose_and_context()
	{
		// Arrange
		var masterKey = RandomNumberGenerator.GetBytes(32);
		var context = "tenant-1"u8;

		// Act
		var derived = _sut.DeriveKey(masterKey, "tenant-key", context);

		// Assert
		derived.ShouldNotBeNull();
		derived.Length.ShouldBe(32);
	}

	[Fact]
	public void Produce_deterministic_output()
	{
		// Arrange
		var masterKey = RandomNumberGenerator.GetBytes(32);

		// Act
		var key1 = _sut.DeriveKey(masterKey, "test-purpose");
		var key2 = _sut.DeriveKey(masterKey, "test-purpose");

		// Assert
		key1.ShouldBe(key2);
	}

	[Fact]
	public void Produce_different_keys_for_different_purposes()
	{
		// Arrange
		var masterKey = RandomNumberGenerator.GetBytes(32);

		// Act
		var key1 = _sut.DeriveKey(masterKey, "purpose-a");
		var key2 = _sut.DeriveKey(masterKey, "purpose-b");

		// Assert
		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void Produce_different_keys_for_different_contexts()
	{
		// Arrange
		var masterKey = RandomNumberGenerator.GetBytes(32);

		// Act
		var key1 = _sut.DeriveKey(masterKey, "same-purpose", "context-a"u8);
		var key2 = _sut.DeriveKey(masterKey, "same-purpose", "context-b"u8);

		// Assert
		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void Produce_different_keys_for_different_master_keys()
	{
		// Arrange
		var masterKey1 = RandomNumberGenerator.GetBytes(32);
		var masterKey2 = RandomNumberGenerator.GetBytes(32);

		// Act
		var key1 = _sut.DeriveKey(masterKey1, "same-purpose");
		var key2 = _sut.DeriveKey(masterKey2, "same-purpose");

		// Assert
		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void Respect_custom_output_length()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new HkdfKeyDerivationOptions
		{
			DefaultOutputLength = 16,
		});
		var sut = new HkdfKeyDeriver(options);
		var masterKey = RandomNumberGenerator.GetBytes(32);

		// Act
		var derived = sut.DeriveKey(masterKey, "test");

		// Assert
		derived.Length.ShouldBe(16);
	}

	[Fact]
	public void Respect_custom_hash_algorithm()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new HkdfKeyDerivationOptions
		{
			HashAlgorithm = HashAlgorithmName.SHA512,
		});
		var sut = new HkdfKeyDeriver(options);
		var masterKey = RandomNumberGenerator.GetBytes(32);

		// Act
		var derived = sut.DeriveKey(masterKey, "test");

		// Assert
		derived.Length.ShouldBe(32); // output length still default 32
	}

	[Fact]
	public void Throw_on_null_master_key()
	{
		Should.Throw<ArgumentNullException>(() => _sut.DeriveKey(null!, "test"));
	}

	[Fact]
	public void Throw_on_null_master_key_with_context()
	{
		Should.Throw<ArgumentNullException>(() => _sut.DeriveKey(null!, "test", ReadOnlySpan<byte>.Empty));
	}

	[Fact]
	public void Throw_on_null_purpose()
	{
		var masterKey = RandomNumberGenerator.GetBytes(32);
		Should.Throw<ArgumentNullException>(() => _sut.DeriveKey(masterKey, null!));
	}

	[Fact]
	public void Throw_on_whitespace_purpose()
	{
		var masterKey = RandomNumberGenerator.GetBytes(32);
		Should.Throw<ArgumentException>(() => _sut.DeriveKey(masterKey, "  "));
	}

	[Fact]
	public void Throw_on_empty_master_key()
	{
		Should.Throw<ArgumentException>(() => _sut.DeriveKey([], "test"));
	}

	[Fact]
	public void Throw_on_empty_master_key_with_context()
	{
		Should.Throw<ArgumentException>(() => _sut.DeriveKey([], "test", ReadOnlySpan<byte>.Empty));
	}

	[Fact]
	public void Throw_on_null_options()
	{
		Should.Throw<ArgumentNullException>(() => new HkdfKeyDeriver(null!));
	}

	[Fact]
	public void Have_default_sha256_hash_algorithm()
	{
		var options = new HkdfKeyDerivationOptions();
		options.HashAlgorithm.ShouldBe(HashAlgorithmName.SHA256);
	}

	[Fact]
	public void Have_default_32_byte_output_length()
	{
		var options = new HkdfKeyDerivationOptions();
		options.DefaultOutputLength.ShouldBe(32);
	}
}
