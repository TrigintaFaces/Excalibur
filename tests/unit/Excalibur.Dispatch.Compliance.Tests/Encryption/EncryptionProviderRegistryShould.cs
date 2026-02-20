using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionProviderRegistryShould
{
	private readonly EncryptionProviderRegistry _sut = new();

	[Fact]
	public void Register_and_retrieve_provider()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();

		// Act
		_sut.Register("provider-1", provider);
		var result = _sut.GetProvider("provider-1");

		// Assert
		result.ShouldBeSameAs(provider);
	}

	[Fact]
	public void Return_null_for_unregistered_provider()
	{
		// Act
		var result = _sut.GetProvider("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_on_duplicate_registration()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("provider-1", provider);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			_sut.Register("provider-1", A.Fake<IEncryptionProvider>()));
	}

	[Fact]
	public void Set_primary_provider()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("primary", provider);

		// Act
		_sut.SetPrimary("primary");
		var result = _sut.GetPrimary();

		// Assert
		result.ShouldBeSameAs(provider);
	}

	[Fact]
	public void Throw_when_getting_primary_without_setting_it()
	{
		// Act & Assert
		Should.Throw<InvalidOperationException>(() => _sut.GetPrimary());
	}

	[Fact]
	public void Throw_when_setting_primary_to_unregistered_provider()
	{
		// Act & Assert
		Should.Throw<InvalidOperationException>(() => _sut.SetPrimary("nonexistent"));
	}

	[Fact]
	public void Get_all_registered_providers()
	{
		// Arrange
		_sut.Register("p1", A.Fake<IEncryptionProvider>());
		_sut.Register("p2", A.Fake<IEncryptionProvider>());

		// Act
		var all = _sut.GetAll();

		// Assert
		all.Count.ShouldBe(2);
	}

	[Fact]
	public void Return_empty_list_when_no_providers()
	{
		// Act
		var all = _sut.GetAll();

		// Assert
		all.Count.ShouldBe(0);
	}

	[Fact]
	public void Get_empty_legacy_providers_by_default()
	{
		// Act
		var legacy = _sut.GetLegacyProviders();

		// Assert
		legacy.Count.ShouldBe(0);
	}

	[Fact]
	public void Case_insensitive_provider_lookup()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("Provider-1", provider);

		// Act
		var result = _sut.GetProvider("provider-1");

		// Assert
		result.ShouldBeSameAs(provider);
	}

	[Fact]
	public void Find_decryption_provider_from_primary()
	{
		// Arrange - use real AesGcmEncryptionProvider since it's sealed and CanDecrypt does type check
		var provider = new AesGcmEncryptionProvider(
			A.Fake<IKeyManagementProvider>(),
			NullLogger<AesGcmEncryptionProvider>.Instance);
		_sut.Register("primary", provider);
		_sut.SetPrimary("primary");

		var encryptedData = new EncryptedData
		{
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Ciphertext = [1, 2, 3],
			Iv = [1, 2, 3],
			AuthTag = [1, 2, 3],
			KeyId = "key-1",
			KeyVersion = 1,
		};

		// Act
		var result = _sut.FindDecryptionProvider(encryptedData);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void Return_null_when_no_provider_can_decrypt()
	{
		// Arrange
		_sut.Register("p1", A.Fake<IEncryptionProvider>());
		_sut.SetPrimary("p1");

		var encryptedData = new EncryptedData
		{
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Ciphertext = [1, 2, 3],
			Iv = [1, 2, 3],
			AuthTag = [1, 2, 3],
			KeyId = "key-1",
			KeyVersion = 1,
		};

		// Act
		var result = _sut.FindDecryptionProvider(encryptedData);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Return_null_for_unsupported_algorithm()
	{
		// Arrange
		_sut.Register("p1", A.Fake<IEncryptionProvider>());
		_sut.SetPrimary("p1");

		var encryptedData = new EncryptedData
		{
			Algorithm = EncryptionAlgorithm.Aes256CbcHmac,
			Ciphertext = [1, 2, 3],
			Iv = [1, 2, 3],
			AuthTag = [1, 2, 3],
			KeyId = "key-1",
			KeyVersion = 1,
		};

		// Act
		var result = _sut.FindDecryptionProvider(encryptedData);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_on_null_provider_id_for_register()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Register(null!, A.Fake<IEncryptionProvider>()));
	}

	[Fact]
	public void Throw_on_null_provider_for_register()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Register("test", null!));
	}

	[Fact]
	public void Throw_on_null_provider_id_for_get_provider()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetProvider(null!));
	}

	[Fact]
	public void Throw_on_null_provider_id_for_set_primary()
	{
		Should.Throw<ArgumentNullException>(() => _sut.SetPrimary(null!));
	}

	[Fact]
	public void Throw_on_null_encrypted_data_for_find()
	{
		Should.Throw<ArgumentNullException>(() => _sut.FindDecryptionProvider(null!));
	}

	[Fact]
	public void Remove_from_legacy_when_setting_as_primary()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("p1", provider);
		_sut.AddLegacyProvider("p1");

		// Act
		_sut.SetPrimary("p1");

		// Assert
		var legacy = _sut.GetLegacyProviders();
		legacy.Count.ShouldBe(0);
	}

	[Fact]
	public void Add_legacy_provider()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("legacy", provider);

		// Act
		_sut.AddLegacyProvider("legacy");

		// Assert
		var legacy = _sut.GetLegacyProviders();
		legacy.Count.ShouldBe(1);
		legacy[0].ShouldBeSameAs(provider);
	}

	[Fact]
	public void Not_add_duplicate_legacy_provider()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("legacy", provider);

		// Act
		_sut.AddLegacyProvider("legacy");
		_sut.AddLegacyProvider("legacy");

		// Assert
		var legacy = _sut.GetLegacyProviders();
		legacy.Count.ShouldBe(1);
	}

	[Fact]
	public void Throw_when_adding_unregistered_legacy_provider()
	{
		Should.Throw<InvalidOperationException>(() => _sut.AddLegacyProvider("nonexistent"));
	}

	[Fact]
	public void Throw_on_null_for_add_legacy_provider()
	{
		Should.Throw<ArgumentNullException>(() => _sut.AddLegacyProvider(null!));
	}
}
