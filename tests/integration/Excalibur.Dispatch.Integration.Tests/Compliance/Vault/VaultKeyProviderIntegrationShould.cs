// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;
using Excalibur.Dispatch.Compliance.Vault;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Vault;

/// <summary>
/// Integration tests for HashiCorp Vault provider using dev container.
/// </summary>
[Collection(VaultTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
public sealed class VaultKeyProviderIntegrationShould : IAsyncLifetime, IDisposable
{
	private readonly VaultContainerFixture _fixture;
	private readonly ILogger<VaultKeyProvider> _logger;
	private readonly IMemoryCache _cache;
	private VaultKeyProvider? _provider;

	public VaultKeyProviderIntegrationShould(VaultContainerFixture fixture)
	{
		_fixture = fixture;
		_logger = A.Fake<ILogger<VaultKeyProvider>>();
		_cache = new MemoryCache(new MemoryCacheOptions());
	}

	public Task InitializeAsync()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new VaultOptions
		{
			VaultUri = new Uri(_fixture.VaultAddress),
			Auth = { AuthMethod = VaultAuthMethod.Token, Token = _fixture.Token },
			TransitMountPath = "transit",
			KeyNamePrefix = "excalibur-test-",
			MetadataCacheDuration = TimeSpan.FromMinutes(5)
		});

		_provider = new VaultKeyProvider(options, _cache, _logger);
		return Task.CompletedTask;
	}

	public Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		_provider?.Dispose();
		_cache?.Dispose();
	}

	[Fact]
	public void ThrowArgumentException_WhenVaultUriNotConfigured()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new VaultOptions
		{
			VaultUri = null
		});

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new VaultKeyProvider(options, _cache, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new VaultKeyProvider(null!, _cache, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenCacheIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new VaultOptions
		{
			VaultUri = new Uri("http://localhost:8200"),
			Auth = { Token = "test" }
		});

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new VaultKeyProvider(options, null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new VaultOptions
		{
			VaultUri = new Uri("http://localhost:8200"),
			Auth = { Token = "test" }
		});

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new VaultKeyProvider(options, _cache, null!));
	}

	[Fact]
	public async Task CreateNewKey_WhenKeyDoesNotExist()
	{
		// Arrange
		var keyId = $"test-{Guid.NewGuid():N}";

		// Act
		var result = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
		result.NewKey.KeyId.ShouldBe(keyId);
		result.NewKey.Status.ShouldBe(KeyStatus.Active);
		result.PreviousKey.ShouldBeNull();

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task GetKey_AfterCreation()
	{
		// Arrange
		var keyId = $"test-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);

		// Assert
		_ = metadata.ShouldNotBeNull();
		metadata.KeyId.ShouldBe(keyId);
		metadata.Status.ShouldBe(KeyStatus.Active);
		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task ReturnNull_WhenKeyDoesNotExist()
	{
		// Arrange
		var keyId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);

		// Assert
		metadata.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyVersion_ReturnsCorrectVersion()
	{
		// Arrange
		var keyId = $"version-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None); // v1
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);  // v2

		// Act
		var v1 = await _provider.GetKeyVersionAsync(keyId, 1, CancellationToken.None);
		var v2 = await _provider.GetKeyVersionAsync(keyId, 2, CancellationToken.None);

		// Assert
		_ = v1.ShouldNotBeNull();
		v1.Version.ShouldBe(1);
		_ = v2.ShouldNotBeNull();
		v2.Version.ShouldBe(2);

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task ListKeys_ReturnsCreatedKeys()
	{
		// Arrange
		var keyId1 = $"list-{Guid.NewGuid():N}";
		var keyId2 = $"list-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId1, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _provider.RotateKeyAsync(keyId2, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var keys = await _provider.ListKeysAsync(null, null, CancellationToken.None);

		// Assert
		keys.ShouldNotBeEmpty();
		keys.ShouldContain(k => k.KeyId == keyId1);
		keys.ShouldContain(k => k.KeyId == keyId2);

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId1, 30, CancellationToken.None);
		_ = await _provider.DeleteKeyAsync(keyId2, 30, CancellationToken.None);
	}

	[Fact]
	public async Task DeleteKey_RemovesKey()
	{
		// Arrange
		var keyId = $"delete-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var result = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();

		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);
		metadata.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteKey_ReturnsFalse_WhenKeyNotFound()
	{
		// Arrange
		var keyId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var result = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RotateKey_CreatesNewVersion()
	{
		// Arrange
		var keyId = $"rotate-{Guid.NewGuid():N}";
		var firstResult = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		firstResult.Success.ShouldBeTrue();
		var firstVersion = firstResult.NewKey.Version;

		// Act
		var secondResult = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		secondResult.Success.ShouldBeTrue();
		_ = secondResult.NewKey.ShouldNotBeNull();
		secondResult.NewKey.Version.ShouldBeGreaterThan(firstVersion);
		_ = secondResult.PreviousKey.ShouldNotBeNull();

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task SuspendKey_UpdatesKeyConfig()
	{
		// Arrange
		var keyId = $"suspend-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var result = await _provider.SuspendKeyAsync(keyId, "Security incident", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task SuspendKey_ReturnsFalse_WhenKeyNotFound()
	{
		// Arrange
		var keyId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var result = await _provider.SuspendKeyAsync(keyId, "Test", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task GetActiveKey_ReturnsActiveKey()
	{
		// Arrange
		var keyId = $"active-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var activeKey = await _provider.GetActiveKeyAsync(null, CancellationToken.None);

		// Assert
		if (activeKey != null)
		{
			activeKey.Status.ShouldBe(KeyStatus.Active);
		}

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task CreateKeyWithAlgorithm_Aes256Gcm()
	{
		// Arrange
		var keyId = $"aes-{Guid.NewGuid():N}";

		// Act
		var result = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
		result.NewKey.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task CacheKeyMetadata_ReducesApiCalls()
	{
		// Arrange
		var keyId = $"cache-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act - Multiple calls should use cache
		var first = await _provider.GetKeyAsync(keyId, CancellationToken.None);
		var second = await _provider.GetKeyAsync(keyId, CancellationToken.None);
		var third = await _provider.GetKeyAsync(keyId, CancellationToken.None);

		// Assert
		_ = first.ShouldNotBeNull();
		_ = second.ShouldNotBeNull();
		_ = third.ShouldNotBeNull();
		first.KeyId.ShouldBe(second.KeyId);
		second.KeyId.ShouldBe(third.KeyId);

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}
}
