// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionProviderRegistry"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionProviderRegistryShould
{
	private readonly EncryptionProviderRegistry _sut;

	public EncryptionProviderRegistryShould()
	{
		_sut = new EncryptionProviderRegistry();
	}

	#region Register Tests

	[Fact]
	public void Register_AddProvider_Successfully()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();

		// Act
		_sut.Register("test-provider", provider);

		// Assert
		var result = _sut.GetProvider("test-provider");
		result.ShouldBe(provider);
	}

	[Fact]
	public void Register_ThrowArgumentNullException_WhenProviderIdIsNull()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.Register(null!, provider));
	}

	[Fact]
	public void Register_ThrowArgumentNullException_WhenProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.Register("test", null!));
	}

	[Fact]
	public void Register_ThrowInvalidOperationException_WhenDuplicateId()
	{
		// Arrange
		var provider1 = A.Fake<IEncryptionProvider>();
		var provider2 = A.Fake<IEncryptionProvider>();
		_sut.Register("duplicate-id", provider1);

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => _sut.Register("duplicate-id", provider2));
		ex.Message.ShouldContain("duplicate-id");
		ex.Message.ShouldContain("already registered");
	}

	[Fact]
	public void Register_BeCaseInsensitive_ForProviderId()
	{
		// Arrange
		var provider1 = A.Fake<IEncryptionProvider>();
		var provider2 = A.Fake<IEncryptionProvider>();
		_sut.Register("Provider-ID", provider1);

		// Act & Assert - should throw because same ID (case-insensitive)
		_ = Should.Throw<InvalidOperationException>(() => _sut.Register("provider-id", provider2));
	}

	#endregion Register Tests

	#region GetProvider Tests

	[Fact]
	public void GetProvider_ReturnProvider_WhenExists()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("my-provider", provider);

		// Act
		var result = _sut.GetProvider("my-provider");

		// Assert
		result.ShouldBe(provider);
	}

	[Fact]
	public void GetProvider_ReturnNull_WhenNotExists()
	{
		// Act
		var result = _sut.GetProvider("non-existent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetProvider_ThrowArgumentNullException_WhenIdIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.GetProvider(null!));
	}

	[Fact]
	public void GetProvider_BeCaseInsensitive()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("MyProvider", provider);

		// Act & Assert
		_sut.GetProvider("myprovider").ShouldBe(provider);
		_sut.GetProvider("MYPROVIDER").ShouldBe(provider);
		_sut.GetProvider("MyProvider").ShouldBe(provider);
	}

	#endregion GetProvider Tests

	#region GetPrimary Tests

	[Fact]
	public void GetPrimary_ThrowInvalidOperationException_WhenNoPrimaryConfigured()
	{
		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => _sut.GetPrimary());
		ex.Message.ShouldContain("No primary provider is configured");
	}

	[Fact]
	public void GetPrimary_ReturnPrimaryProvider_WhenConfigured()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("primary", provider);
		_sut.SetPrimary("primary");

		// Act
		var result = _sut.GetPrimary();

		// Assert
		result.ShouldBe(provider);
	}

	#endregion GetPrimary Tests

	#region SetPrimary Tests

	[Fact]
	public void SetPrimary_SetPrimaryProvider()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("provider-1", provider);

		// Act
		_sut.SetPrimary("provider-1");

		// Assert
		_sut.GetPrimary().ShouldBe(provider);
	}

	[Fact]
	public void SetPrimary_ThrowArgumentNullException_WhenIdIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.SetPrimary(null!));
	}

	[Fact]
	public void SetPrimary_ThrowInvalidOperationException_WhenProviderNotRegistered()
	{
		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => _sut.SetPrimary("non-existent"));
		ex.Message.ShouldContain("non-existent");
		ex.Message.ShouldContain("not registered");
	}

	[Fact]
	public void SetPrimary_ReplacePreviousPrimary()
	{
		// Arrange
		var provider1 = A.Fake<IEncryptionProvider>();
		var provider2 = A.Fake<IEncryptionProvider>();
		_sut.Register("provider-1", provider1);
		_sut.Register("provider-2", provider2);
		_sut.SetPrimary("provider-1");

		// Act
		_sut.SetPrimary("provider-2");

		// Assert
		_sut.GetPrimary().ShouldBe(provider2);
	}

	[Fact]
	public void SetPrimary_RemoveFromLegacyList_IfPresent()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("my-provider", provider);
		_sut.AddLegacyProvider("my-provider");

		// Verify it's in legacy list
		_sut.GetLegacyProviders().ShouldContain(provider);

		// Act - promote to primary
		_sut.SetPrimary("my-provider");

		// Assert - should be removed from legacy
		_sut.GetLegacyProviders().ShouldNotContain(provider);
		_sut.GetPrimary().ShouldBe(provider);
	}

	#endregion SetPrimary Tests

	#region GetLegacyProviders Tests

	[Fact]
	public void GetLegacyProviders_ReturnEmptyList_WhenNone()
	{
		// Act
		var result = _sut.GetLegacyProviders();

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetLegacyProviders_ReturnLegacyProviders()
	{
		// Arrange
		var legacy1 = A.Fake<IEncryptionProvider>();
		var legacy2 = A.Fake<IEncryptionProvider>();
		_sut.Register("legacy-1", legacy1);
		_sut.Register("legacy-2", legacy2);
		_sut.AddLegacyProvider("legacy-1");
		_sut.AddLegacyProvider("legacy-2");

		// Act
		var result = _sut.GetLegacyProviders();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain(legacy1);
		result.ShouldContain(legacy2);
	}

	[Fact]
	public void GetLegacyProviders_ReturnInReverseAdditionOrder()
	{
		// Arrange - add legacy1 first, then legacy2
		var legacy1 = A.Fake<IEncryptionProvider>();
		var legacy2 = A.Fake<IEncryptionProvider>();
		_sut.Register("legacy-1", legacy1);
		_sut.Register("legacy-2", legacy2);
		_sut.AddLegacyProvider("legacy-1");
		_sut.AddLegacyProvider("legacy-2");

		// Act
		var result = _sut.GetLegacyProviders();

		// Assert - most recently added should be first
		result[0].ShouldBe(legacy2);
		result[1].ShouldBe(legacy1);
	}

	#endregion GetLegacyProviders Tests

	#region AddLegacyProvider Tests (Internal)

	[Fact]
	public void AddLegacyProvider_AddToLegacyList()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("legacy", provider);

		// Act
		_sut.AddLegacyProvider("legacy");

		// Assert
		_sut.GetLegacyProviders().ShouldContain(provider);
	}

	[Fact]
	public void AddLegacyProvider_ThrowIfNotRegistered()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _sut.AddLegacyProvider("non-existent"));
	}

	[Fact]
	public void AddLegacyProvider_NotAddDuplicate()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("legacy", provider);
		_sut.AddLegacyProvider("legacy");

		// Act - add again
		_sut.AddLegacyProvider("legacy");

		// Assert - should still have only one entry
		_sut.GetLegacyProviders().Count.ShouldBe(1);
	}

	#endregion AddLegacyProvider Tests (Internal)

	#region GetAll Tests

	[Fact]
	public void GetAll_ReturnEmptyList_WhenNoProviders()
	{
		// Act
		var result = _sut.GetAll();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetAll_ReturnAllProviders()
	{
		// Arrange
		var p1 = A.Fake<IEncryptionProvider>();
		var p2 = A.Fake<IEncryptionProvider>();
		var p3 = A.Fake<IEncryptionProvider>();
		_sut.Register("p1", p1);
		_sut.Register("p2", p2);
		_sut.Register("p3", p3);

		// Act
		var result = _sut.GetAll();

		// Assert
		result.Count.ShouldBe(3);
		result.ShouldContain(p1);
		result.ShouldContain(p2);
		result.ShouldContain(p3);
	}

	#endregion GetAll Tests

	#region FindDecryptionProvider Tests

	[Fact]
	public void FindDecryptionProvider_ThrowArgumentNullException_WhenDataIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.FindDecryptionProvider(null!));
	}

	[Fact]
	public void FindDecryptionProvider_ReturnNull_WhenNoProvidersRegistered()
	{
		// Arrange
		var data = CreateEncryptedData(EncryptionAlgorithm.Aes256Gcm);

		// Act
		var result = _sut.FindDecryptionProvider(data);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void FindDecryptionProvider_PreferPrimaryProvider_ForMatchingAlgorithm()
	{
		// Arrange
		var primary = CreateAesGcmProvider();
		var legacy = CreateAesGcmProvider();
		_sut.Register("primary", primary);
		_sut.Register("legacy", legacy);
		_sut.SetPrimary("primary");
		_sut.AddLegacyProvider("legacy");

		var data = CreateEncryptedData(EncryptionAlgorithm.Aes256Gcm);

		// Act
		var result = _sut.FindDecryptionProvider(data);

		// Assert
		result.ShouldBe(primary);
	}

	[Fact]
	public void FindDecryptionProvider_FallbackToLegacy_WhenPrimaryCannotDecrypt()
	{
		// Arrange
		var primary = A.Fake<IEncryptionProvider>(); // Not AesGcmProvider, can't handle Aes256Gcm
		var legacy = CreateAesGcmProvider();
		_sut.Register("primary", primary);
		_sut.Register("legacy", legacy);
		_sut.SetPrimary("primary");
		_sut.AddLegacyProvider("legacy");

		var data = CreateEncryptedData(EncryptionAlgorithm.Aes256Gcm);

		// Act
		var result = _sut.FindDecryptionProvider(data);

		// Assert
		result.ShouldBe(legacy);
	}

	[Fact]
	public void FindDecryptionProvider_ReturnNull_ForUnsupportedAlgorithm()
	{
		// Arrange
		var provider = CreateAesGcmProvider();
		_sut.Register("provider", provider);
		_sut.SetPrimary("provider");

		var data = CreateEncryptedData((EncryptionAlgorithm)99);

		// Act
		var result = _sut.FindDecryptionProvider(data);

		// Assert
		result.ShouldBeNull();
	}

	#endregion FindDecryptionProvider Tests

	#region Thread Safety Tests

	[Fact]
	public async Task BeThreadSafe_ForConcurrentRegistrations()
	{
		// Arrange
		var tasks = Enumerable.Range(0, 100).Select(i =>
			Task.Run(() =>
			{
				var provider = A.Fake<IEncryptionProvider>();
				_sut.Register($"provider-{i}", provider);
			}));

		// Act & Assert - should not throw
		await Task.WhenAll(tasks);
		_sut.GetAll().Count.ShouldBe(100);
	}

	[Fact]
	public async Task BeThreadSafe_ForConcurrentReads()
	{
		// Arrange
		var provider = A.Fake<IEncryptionProvider>();
		_sut.Register("provider", provider);
		_sut.SetPrimary("provider");

		var tasks = Enumerable.Range(0, 100).Select(_ =>
			Task.Run(() =>
			{
				var p = _sut.GetPrimary();
				var all = _sut.GetAll();
				var byId = _sut.GetProvider("provider");
				return (p, all, byId);
			}));

		// Act
		var results = await Task.WhenAll(tasks);

		// Assert
		foreach (var (p, all, byId) in results)
		{
			p.ShouldBe(provider);
			all.Count.ShouldBe(1);
			byId.ShouldBe(provider);
		}
	}

	#endregion Thread Safety Tests

	#region Helpers

	private static EncryptedData CreateEncryptedData(EncryptionAlgorithm algorithm)
	{
		return new EncryptedData
		{
			Ciphertext = new byte[] { 1, 2, 3, 4 },
			KeyId = "test-key",
			KeyVersion = 1,
			Algorithm = algorithm,
			Iv = new byte[12],
			AuthTag = new byte[16]
		};
	}

	/// <summary>
	/// Creates an AesGcmEncryptionProvider instance for testing CanDecrypt type matching.
	/// </summary>
	private static AesGcmEncryptionProvider CreateAesGcmProvider()
	{
		return new AesGcmEncryptionProvider(
			A.Fake<IKeyManagementProvider>(),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<AesGcmEncryptionProvider>.Instance);
	}

	#endregion Helpers
}
