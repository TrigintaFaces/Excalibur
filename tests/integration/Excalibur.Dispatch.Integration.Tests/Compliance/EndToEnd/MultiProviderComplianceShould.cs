// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Amazon.KeyManagementService;

using Excalibur.Dispatch.Compliance.Aws;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;
using Excalibur.Dispatch.Compliance.Vault;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

// Encryption types are now in Excalibur.Dispatch.Compliance directly (namespace flattened)

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.EndToEnd;

/// <summary>
/// End-to-end tests for multi-provider compliance scenarios.
/// Tests interoperability between different key management providers.
/// </summary>
[Collection(ComplianceMultiContainerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
public sealed class MultiProviderComplianceShould : IAsyncLifetime, IDisposable
{
	private readonly ComplianceMultiContainerFixture _fixture;
	private readonly ILogger<AwsKmsProvider> _awsLogger;
	private readonly ILogger<VaultKeyProvider> _vaultLogger;
	private readonly IMemoryCache _cache;
	private IAmazonKeyManagementService? _kmsClient;
	private AwsKmsProvider? _awsProvider;
	private VaultKeyProvider? _vaultProvider;

	public MultiProviderComplianceShould(ComplianceMultiContainerFixture fixture)
	{
		_fixture = fixture;
		_awsLogger = A.Fake<ILogger<AwsKmsProvider>>();
		_vaultLogger = A.Fake<ILogger<VaultKeyProvider>>();
		_cache = new MemoryCache(new MemoryCacheOptions());
	}

	public Task InitializeAsync()
	{
		// Initialize AWS KMS via LocalStack
		_kmsClient = _fixture.LocalStack.CreateKmsClient();
		var awsOptions = Microsoft.Extensions.Options.Options.Create(new AwsKmsOptions
		{
			KeyAliasPrefix = "excalibur-e2e",
			Environment = "test",
			EnableAutoRotation = false,
			DefaultKeySpec = KeySpec.SYMMETRIC_DEFAULT
		});
		_awsProvider = new AwsKmsProvider(_kmsClient, awsOptions, _awsLogger, _cache);

		// Initialize Vault
		var vaultOptions = Microsoft.Extensions.Options.Options.Create(new VaultOptions
		{
			VaultUri = new Uri(_fixture.Vault.VaultAddress),
			Auth = { AuthMethod = VaultAuthMethod.Token, Token = _fixture.Vault.Token },
			TransitMountPath = "transit",
			KeyNamePrefix = "excalibur-e2e-",
			MetadataCacheDuration = TimeSpan.FromMinutes(5)
		});
		_vaultProvider = new VaultKeyProvider(vaultOptions, _cache, _vaultLogger);

		return Task.CompletedTask;
	}

	public Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		_awsProvider?.Dispose();
		_vaultProvider?.Dispose();
		_kmsClient?.Dispose();
		_cache?.Dispose();
	}

	[Fact]
	public async Task CreateKeysAcrossMultipleProviders()
	{
		// Arrange
		var awsKeyId = $"aws-multi-{Guid.NewGuid():N}";
		var vaultKeyId = $"vault-multi-{Guid.NewGuid():N}";

		// Act
		var awsResult = await _awsProvider.RotateKeyAsync(awsKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		var vaultResult = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		awsResult.Success.ShouldBeTrue();
		vaultResult.Success.ShouldBeTrue();

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(vaultKeyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task MaintainConsistentKeyMetadataFormat()
	{
		// Arrange
		var awsKeyId = $"aws-metadata-{Guid.NewGuid():N}";
		var vaultKeyId = $"vault-metadata-{Guid.NewGuid():N}";

		_ = await _awsProvider.RotateKeyAsync(awsKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var awsMetadata = await _awsProvider.GetKeyAsync(awsKeyId, CancellationToken.None);
		var vaultMetadata = await _vaultProvider.GetKeyAsync(vaultKeyId, CancellationToken.None);

		// Assert - Both should have consistent metadata format
		_ = awsMetadata.ShouldNotBeNull();
		_ = vaultMetadata.ShouldNotBeNull();

		awsMetadata.KeyId.ShouldNotBeNullOrEmpty();
		vaultMetadata.KeyId.ShouldNotBeNullOrEmpty();

		awsMetadata.Status.ShouldBe(KeyStatus.Active);
		vaultMetadata.Status.ShouldBe(KeyStatus.Active);

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(vaultKeyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task SupportKeyRotationAcrossProviders()
	{
		// Arrange
		var awsKeyId = $"aws-rotate-{Guid.NewGuid():N}";
		var vaultKeyId = $"vault-rotate-{Guid.NewGuid():N}";

		_ = await _awsProvider.RotateKeyAsync(awsKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act - Rotate both
		var awsRotation = await _awsProvider.RotateKeyAsync(awsKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		var vaultRotation = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		awsRotation.Success.ShouldBeTrue();
		_ = awsRotation.PreviousKey.ShouldNotBeNull();

		vaultRotation.Success.ShouldBeTrue();
		_ = vaultRotation.PreviousKey.ShouldNotBeNull();

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(vaultKeyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task HandleProviderSpecificAlgorithms()
	{
		// Arrange - Vault supports AES-256-GCM algorithm
		var aesKeyId = $"vault-aes-{Guid.NewGuid():N}";

		// Act
		var aesResult = await _vaultProvider.RotateKeyAsync(aesKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		aesResult.Success.ShouldBeTrue();
		aesResult.NewKey.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(aesKeyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task MaintainKeyVersionHistory()
	{
		// Arrange
		var keyId = $"vault-history-{Guid.NewGuid():N}";

		// Create and rotate multiple times
		_ = await _vaultProvider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None); // v1
		_ = await _vaultProvider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);   // v2
		_ = await _vaultProvider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);   // v3

		// Act
		var v1 = await _vaultProvider.GetKeyVersionAsync(keyId, 1, CancellationToken.None);
		var v2 = await _vaultProvider.GetKeyVersionAsync(keyId, 2, CancellationToken.None);
		var v3 = await _vaultProvider.GetKeyVersionAsync(keyId, 3, CancellationToken.None);

		// Assert
		_ = v1.ShouldNotBeNull();
		v1.Version.ShouldBe(1);

		_ = v2.ShouldNotBeNull();
		v2.Version.ShouldBe(2);

		_ = v3.ShouldNotBeNull();
		v3.Version.ShouldBe(3);

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(keyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task SupportKeySuspensionAcrossProviders()
	{
		// Arrange
		var awsKeyId = $"aws-suspend-{Guid.NewGuid():N}";
		var vaultKeyId = $"vault-suspend-{Guid.NewGuid():N}";

		_ = await _awsProvider.RotateKeyAsync(awsKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var awsSuspend = await _awsProvider.SuspendKeyAsync(awsKeyId, "Security test", CancellationToken.None);
		var vaultSuspend = await _vaultProvider.SuspendKeyAsync(vaultKeyId, "Security test", CancellationToken.None);

		// Assert
		awsSuspend.ShouldBeTrue();
		vaultSuspend.ShouldBeTrue();

		var awsStatus = await _awsProvider.GetKeyAsync(awsKeyId, CancellationToken.None);
		_ = awsStatus.ShouldNotBeNull();
		awsStatus.Status.ShouldBe(KeyStatus.Suspended);

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(vaultKeyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task ListKeysFromMultipleProviders()
	{
		// Arrange - Create keys in both providers
		var awsKeyId = $"aws-list-{Guid.NewGuid():N}";
		var vaultKeyId = $"vault-list-{Guid.NewGuid():N}";

		_ = await _awsProvider.RotateKeyAsync(awsKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var awsKeys = await _awsProvider.ListKeysAsync(null, null, CancellationToken.None);
		var vaultKeys = await _vaultProvider.ListKeysAsync(null, null, CancellationToken.None);

		// Assert
		awsKeys.ShouldContain(k => k.KeyId == awsKeyId);
		vaultKeys.ShouldContain(k => k.KeyId == vaultKeyId);

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(vaultKeyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task HandleConcurrentOperationsAcrossProviders()
	{
		// Arrange
		var awsTasks = Enumerable.Range(0, 5)
			.Select(i => _awsProvider.RotateKeyAsync($"aws-concurrent-{i}-{Guid.NewGuid():N}", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None));

		var vaultTasks = Enumerable.Range(0, 5)
			.Select(i => _vaultProvider.RotateKeyAsync($"vault-concurrent-{i}-{Guid.NewGuid():N}", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None));

		// Act - Run all concurrently
		var results = await Task.WhenAll(awsTasks.Concat(vaultTasks));

		// Assert
		results.All(r => r.Success).ShouldBeTrue();

		// Cleanup - Delete vault keys
		foreach (var result in results.Where(r => r.NewKey?.KeyId.StartsWith("vault-") == true))
		{
			_ = await _vaultProvider.DeleteKeyAsync(result.NewKey.KeyId, 30, CancellationToken.None);
		}
	}

	[Fact]
	public async Task ValidateCachingBehaviorAcrossProviders()
	{
		// Arrange
		var awsKeyId = $"aws-cache-{Guid.NewGuid():N}";
		var vaultKeyId = $"vault-cache-{Guid.NewGuid():N}";

		_ = await _awsProvider.RotateKeyAsync(awsKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act - Multiple reads should use cache
		var awsReads = new List<KeyMetadata?>();
		var vaultReads = new List<KeyMetadata?>();

		for (var i = 0; i < 5; i++)
		{
			awsReads.Add(await _awsProvider.GetKeyAsync(awsKeyId, CancellationToken.None));
			vaultReads.Add(await _vaultProvider.GetKeyAsync(vaultKeyId, CancellationToken.None));
		}

		// Assert - All reads should return consistent data
		awsReads.All(r => r?.KeyId == awsKeyId).ShouldBeTrue();
		vaultReads.All(r => r?.KeyId == vaultKeyId).ShouldBeTrue();

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(vaultKeyId, 30, CancellationToken.None);
	}

	[Fact]
	public async Task HandleNonExistentKeysConsistently()
	{
		// Arrange
		var nonExistentId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var awsResult = await _awsProvider.GetKeyAsync(nonExistentId, CancellationToken.None);
		var vaultResult = await _vaultProvider.GetKeyAsync(nonExistentId, CancellationToken.None);

		// Assert - Both should return null consistently
		awsResult.ShouldBeNull();
		vaultResult.ShouldBeNull();
	}

	[Fact]
	public async Task SupportPurposeBasedKeySelectionAcrossProviders()
	{
		// Arrange
		var awsPurposeKey = $"aws-purpose-{Guid.NewGuid():N}";
		var vaultPurposeKey = $"vault-purpose-{Guid.NewGuid():N}";

		// Act
		_ = await _awsProvider.RotateKeyAsync(awsPurposeKey, EncryptionAlgorithm.Aes256Gcm, "pii-encryption", null, CancellationToken.None);
		_ = await _vaultProvider.RotateKeyAsync(vaultPurposeKey, EncryptionAlgorithm.Aes256Gcm, "pii-encryption", null, CancellationToken.None);

		var awsKey = await _awsProvider.GetKeyAsync(awsPurposeKey, cancellationToken: CancellationToken.None);
		var vaultKey = await _vaultProvider.GetKeyAsync(vaultPurposeKey, cancellationToken: CancellationToken.None);

		// Assert
		_ = awsKey.ShouldNotBeNull();
		_ = vaultKey.ShouldNotBeNull();

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(vaultPurposeKey, 30, CancellationToken.None);
	}

	[Fact]
	public async Task DeleteKeysAcrossProviders()
	{
		// Arrange
		var vaultKeyId = $"vault-delete-{Guid.NewGuid():N}";
		_ = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var vaultDelete = await _vaultProvider.DeleteKeyAsync(vaultKeyId, 30, CancellationToken.None);

		// Assert
		vaultDelete.ShouldBeTrue();

		var vaultKey = await _vaultProvider.GetKeyAsync(vaultKeyId, CancellationToken.None);
		vaultKey.ShouldBeNull();
	}

	[Fact]
	public async Task HandleDeleteNonExistentKeyConsistently()
	{
		// Arrange
		var nonExistentId = $"nonexistent-delete-{Guid.NewGuid():N}";

		// Act
		var awsResult = await _awsProvider.DeleteKeyAsync(nonExistentId, 30, CancellationToken.None);
		var vaultResult = await _vaultProvider.DeleteKeyAsync(nonExistentId, 30, CancellationToken.None);

		// Assert - Both should return false consistently
		awsResult.ShouldBeFalse();
		vaultResult.ShouldBeFalse();
	}

	[Fact]
	public async Task HandleSuspendNonExistentKeyConsistently()
	{
		// Arrange
		var nonExistentId = $"nonexistent-suspend-{Guid.NewGuid():N}";

		// Act
		var awsResult = await _awsProvider.SuspendKeyAsync(nonExistentId, "Test", CancellationToken.None);
		var vaultResult = await _vaultProvider.SuspendKeyAsync(nonExistentId, "Test", CancellationToken.None);

		// Assert - Both should return false consistently
		awsResult.ShouldBeFalse();
		vaultResult.ShouldBeFalse();
	}

	[Fact]
	public async Task GetActiveKeyFromProviders()
	{
		// Arrange - Create keys in both providers
		_ = await _awsProvider.RotateKeyAsync("default", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		var vaultKeyId = $"vault-active-{Guid.NewGuid():N}";
		_ = await _vaultProvider.RotateKeyAsync(vaultKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var awsActive = await _awsProvider.GetActiveKeyAsync(null, CancellationToken.None);
		var vaultActive = await _vaultProvider.GetActiveKeyAsync(null, CancellationToken.None);

		// Assert
		_ = awsActive.ShouldNotBeNull();
		awsActive.Status.ShouldBe(KeyStatus.Active);

		if (vaultActive != null)
		{
			vaultActive.Status.ShouldBe(KeyStatus.Active);
		}

		// Cleanup
		_ = await _vaultProvider.DeleteKeyAsync(vaultKeyId, 30, CancellationToken.None);
	}
}
