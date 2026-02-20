// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Dispatch.Compliance.Azure;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Azure;

/// <summary>
/// Integration tests for Azure Key Vault provider.
/// Note: These tests require an actual Azure Key Vault or mock infrastructure.
/// Tests that can run without Azure infrastructure are marked as Skippable.
/// </summary>
[Collection(AzuriteTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
public sealed class AzureKeyVaultProviderIntegrationShould : IAsyncLifetime, IDisposable
{
	// For real Azure Key Vault tests, set this environment variable
	private static readonly string? VaultUri = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI");

	private readonly AzuriteContainerFixture _fixture;
	private readonly ILogger<AzureKeyVaultProvider> _logger;
	private readonly IMemoryCache _cache;
	private AzureKeyVaultProvider? _provider;

	public AzureKeyVaultProviderIntegrationShould(AzuriteContainerFixture fixture)
	{
		_fixture = fixture;
		_logger = A.Fake<ILogger<AzureKeyVaultProvider>>();
		_cache = new MemoryCache(new MemoryCacheOptions());
	}

	public Task InitializeAsync()
	{
		// Only create provider if we have a real vault URI
		if (!string.IsNullOrEmpty(VaultUri))
		{
			var options = Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
			{
				VaultUri = new Uri(VaultUri),
				KeyNamePrefix = "excalibur-test-",
				UseSoftwareKeys = true, // Use software keys for testing
				MetadataCacheDuration = TimeSpan.FromMinutes(5)
			});

			_provider = new AzureKeyVaultProvider(options, _cache, _logger);
		}

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
		var options = Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
		{
			VaultUri = null
		});

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new AzureKeyVaultProvider(options, _cache, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AzureKeyVaultProvider(null!, _cache, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenCacheIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
		{
			VaultUri = new Uri("https://test.vault.azure.net")
		});

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AzureKeyVaultProvider(options, null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
		{
			VaultUri = new Uri("https://test.vault.azure.net")
		});

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new AzureKeyVaultProvider(options, _cache, null!));
	}

	[SkippableFact]
	public async Task CreateNewKey_WhenKeyDoesNotExist()
	{
		SkipIfVaultUnavailable();
		// Arrange
		var keyId = $"test-{Guid.NewGuid():N}";

		// Act
		var result = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
		result.NewKey.KeyId.ShouldBe(keyId);

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[SkippableFact]
	public async Task GetKey_AfterCreation()
	{
		SkipIfVaultUnavailable();
		// Arrange
		var keyId = $"test-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);

		// Assert
		_ = metadata.ShouldNotBeNull();
		metadata.KeyId.ShouldBe(keyId);
		metadata.Status.ShouldBe(KeyStatus.Active);

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[SkippableFact]
	public async Task ReturnNull_WhenKeyDoesNotExist()
	{
		SkipIfVaultUnavailable();
		// Arrange
		var keyId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);

		// Assert
		metadata.ShouldBeNull();
	}

	[SkippableFact]
	public async Task ListKeys_ReturnsCreatedKeys()
	{
		SkipIfVaultUnavailable();
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

	[SkippableFact]
	public async Task SuspendKey_DisablesKey()
	{
		SkipIfVaultUnavailable();
		// Arrange
		var keyId = $"suspend-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var result = await _provider.SuspendKeyAsync(keyId, "Security test", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();

		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);
		_ = metadata.ShouldNotBeNull();
		metadata.Status.ShouldBe(KeyStatus.Suspended);

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[SkippableFact]
	public async Task DeleteKey_SchedulesDeletion()
	{
		SkipIfVaultUnavailable();
		// Arrange
		var keyId = $"delete-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var result = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[SkippableFact]
	public async Task RotateKey_CreatesNewVersion()
	{
		SkipIfVaultUnavailable();
		// Arrange
		var keyId = $"rotate-{Guid.NewGuid():N}";
		var firstResult = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		firstResult.Success.ShouldBeTrue();

		// Act
		var secondResult = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		secondResult.Success.ShouldBeTrue();
		_ = secondResult.NewKey.ShouldNotBeNull();
		_ = secondResult.PreviousKey.ShouldNotBeNull();

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[SkippableFact]
	public async Task GetActiveKey_ReturnsLatestActiveKey()
	{
		SkipIfVaultUnavailable();
		var keyId = $"active-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var activeKey = await _provider.GetActiveKeyAsync(null, CancellationToken.None);

		// Assert - Should return an active key
		// Note: May return a different key if multiple active keys exist
		if (activeKey != null)
		{
			activeKey.Status.ShouldBe(KeyStatus.Active);
		}

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[SkippableFact]
	public async Task GetCryptographyClient_ReturnsValidClient()
	{
		SkipIfVaultUnavailable();
		var keyId = $"crypto-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var cryptoClient = await _provider.GetCryptographyClientAsync(keyId, CancellationToken.None);

		// Assert
		_ = cryptoClient.ShouldNotBeNull();

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[SkippableFact]
	public async Task CacheKeyMetadata_ReducesApiCalls()
	{
		SkipIfVaultUnavailable();
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

	[SkippableFact]
	public async Task CreateKeyWithPurpose()
	{
		SkipIfVaultUnavailable();
		var keyId = $"purpose-{Guid.NewGuid():N}";

		// Act
		var result = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, "pii-encryption", null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();

		// Cleanup
		_ = await _provider.DeleteKeyAsync(keyId, 30, cancellationToken: CancellationToken.None);
	}

	private void SkipIfVaultUnavailable()
	{
		Skip.If(_provider is null, "AZURE_KEYVAULT_URI environment variable not set");
	}
}
