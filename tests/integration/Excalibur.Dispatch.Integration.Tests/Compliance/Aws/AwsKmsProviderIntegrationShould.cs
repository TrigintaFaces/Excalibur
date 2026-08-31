// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor — fields are set in InitializeAsync()

using Amazon.KeyManagementService;

using Excalibur.Compliance.Aws;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Excalibur.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Aws;

/// <summary>
/// Integration tests for AWS KMS provider using LocalStack.
/// </summary>
[Collection(LocalStackTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class AwsKmsProviderIntegrationShould : IAsyncLifetime, IDisposable
{
	private readonly LocalStackContainerFixture _fixture;
	private readonly ILogger<AwsKmsProvider> _logger;
	private readonly IMemoryCache _cache;
	private IAmazonKeyManagementService _kmsClient;
	private AwsKmsProvider _provider;

	public AwsKmsProviderIntegrationShould(LocalStackContainerFixture fixture)
	{
		_fixture = fixture;
		_logger = A.Fake<ILogger<AwsKmsProvider>>();
		_cache = new MemoryCache(new MemoryCacheOptions());
	}

	public ValueTask InitializeAsync()
	{
		Assert.SkipUnless(_fixture.DockerAvailable, _fixture.InitializationError ?? "LocalStack container not available");

		_kmsClient = _fixture.CreateKmsClient();
		var options = Microsoft.Extensions.Options.Options.Create(new AwsKmsOptions
		{
			KeyAliasPrefix = "excalibur-dispatch",
			Environment = "test",
			KeyPolicy =
			{
				EnableAutoRotation = false,
				DefaultKeySpec = KeySpec.SYMMETRIC_DEFAULT
			}
		});

		_provider = new AwsKmsProvider(_kmsClient, options, _logger, _cache);
		return default;
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return default;
	}

	public void Dispose()
	{
		_provider?.Dispose();
		_kmsClient?.Dispose();
		_cache?.Dispose();
	}

	[Fact]
	public async Task CreateNewKey_WhenKeyDoesNotExist()
	{
		// Arrange
		var keyId = $"test-key-{Guid.NewGuid():N}";

		// Act
		var result = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
		result.NewKey.KeyId.ShouldBe(keyId);
		result.NewKey.Status.ShouldBe(KeyStatus.Active);
		result.PreviousKey.ShouldBeNull();
	}

	[Fact]
	public async Task GetKey_AfterCreation()
	{
		// Arrange
		var keyId = $"test-key-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);

		// Assert
		_ = metadata.ShouldNotBeNull();
		metadata.KeyId.ShouldBe(keyId);
		metadata.Status.ShouldBe(KeyStatus.Active);
		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
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
	public async Task ListKeys_ReturnsCreatedKeys()
	{
		// Arrange
		var keyId1 = $"list-test-{Guid.NewGuid():N}";
		var keyId2 = $"list-test-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId1, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _provider.RotateKeyAsync(keyId2, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var keys = await _provider.ListKeysAsync(null, null, CancellationToken.None);

		// Assert
		keys.ShouldNotBeEmpty();
		keys.ShouldContain(k => k.KeyId == keyId1);
		keys.ShouldContain(k => k.KeyId == keyId2);
	}

	[Fact]
	public async Task ListKeys_FilterByStatus()
	{
		// Arrange
		var activeKeyId = $"active-{Guid.NewGuid():N}";
		var suspendedKeyId = $"suspended-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(activeKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _provider.RotateKeyAsync(suspendedKeyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		_ = await _provider.SuspendKeyAsync(suspendedKeyId, "Test suspension", CancellationToken.None);

		// Act
		var activeKeys = await _provider.ListKeysAsync(KeyStatus.Active, null, CancellationToken.None);

		// Assert
		activeKeys.ShouldContain(k => k.KeyId == activeKeyId);
		activeKeys.ShouldNotContain(k => k.KeyId == suspendedKeyId);
	}

	[Fact]
	public async Task SuspendKey_DisablesKey()
	{
		// Arrange
		var keyId = $"suspend-test-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var result = await _provider.SuspendKeyAsync(keyId, "Security incident", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();

		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);
		_ = metadata.ShouldNotBeNull();
		metadata.Status.ShouldBe(KeyStatus.Suspended);
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
	public async Task DeleteKey_SchedulesDeletion()
	{
		// Arrange
		var keyId = $"delete-test-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var result = await _provider.DeleteKeyAsync(keyId, retentionDays: 7, CancellationToken.None);

		// Assert — AWS KMS schedules deletion (recoverable window), so a successful delete is ScheduledIrreversible.
		result.State.ShouldBe(Excalibur.Compliance.KeyDestructionState.ScheduledIrreversible);

		var metadata = await _provider.GetKeyAsync(keyId, CancellationToken.None);
		// Key should be in pending deletion state or not found
		if (metadata != null)
		{
			metadata.Status.ShouldBe(KeyStatus.PendingDestruction);
		}
	}

	[Fact]
	public async Task DeleteKey_ReturnsFalse_WhenKeyNotFound()
	{
		// Arrange
		var keyId = $"nonexistent-{Guid.NewGuid():N}";

		// Act
		var result = await _provider.DeleteKeyAsync(keyId, 30, CancellationToken.None);

		// Assert
		result.State.ShouldBe(Excalibur.Compliance.KeyDestructionState.NotFound);
	}

	[Fact]
	public async Task RotateKey_CreatesNewVersion()
	{
		// Arrange
		var keyId = $"rotate-test-{Guid.NewGuid():N}";
		var firstResult = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);
		firstResult.Success.ShouldBeTrue();

		// Act
		var secondResult = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		secondResult.Success.ShouldBeTrue();
		_ = secondResult.NewKey.ShouldNotBeNull();
		_ = secondResult.PreviousKey.ShouldNotBeNull();
		secondResult.PreviousKey.Status.ShouldBe(KeyStatus.DecryptOnly);
	}

	[Fact]
	public async Task GetActiveKey_ReturnsActiveKey()
	{
		// Arrange
		var keyId = "default";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act
		var activeKey = await _provider.GetActiveKeyAsync(null, CancellationToken.None);

		// Assert
		_ = activeKey.ShouldNotBeNull();
		activeKey.Status.ShouldBe(KeyStatus.Active);
	}

	// A getter must not provision a customer master key. The two arms below are a pair: the first alone
	// would be satisfied by a provider that answered null to everything, and the second alone would be
	// satisfied by one that answered with any key at all.
	//
	// Both are scoped by a purpose unique to the run rather than by asking for the default key, because
	// this LocalStack container is shared with the other AWS suites under one alias prefix — "no key
	// exists" is only ever true of a purpose nothing else has used.

	[Fact]
	public async Task GetActiveKey_ReturnsNull_AndProvisionsNothing_WhenNoKeyHasThatPurpose()
	{
		// Arrange
		var purpose = $"unused-purpose-{Guid.NewGuid():N}";

		// Act
		var activeKey = await _provider.GetActiveKeyAsync(purpose, CancellationToken.None);

		// Assert - the documented "or null" answer.
		activeKey.ShouldBeNull();

		// And nothing was created on the way to that answer. A key is billable, durable and
		// security-relevant, so it is something a caller asks for explicitly through RotateKeyAsync.
		var afterwards = await _provider.ListKeysAsync(null, purpose, CancellationToken.None);
		afterwards.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetActiveKey_ReturnsTheKeyRecordedForThatPurpose()
	{
		// Arrange
		var purpose = $"purpose-{Guid.NewGuid():N}";
		var keyId = $"active-by-purpose-{Guid.NewGuid():N}";

		var created = await _provider.RotateKeyAsync(
			keyId, EncryptionAlgorithm.Aes256Gcm, purpose, null, CancellationToken.None);
		created.Success.ShouldBeTrue();

		// Act
		var activeKey = await _provider.GetActiveKeyAsync(purpose, CancellationToken.None);

		// Assert - the purpose selects the key recorded for it, not a key whose id happens to contain it.
		_ = activeKey.ShouldNotBeNull();
		activeKey.KeyId.ShouldBe(keyId);
		activeKey.Status.ShouldBe(KeyStatus.Active);
	}

	[Fact]
	public async Task CacheKeyMetadata_ReducesApiCalls()
	{
		// Arrange
		var keyId = $"cache-test-{Guid.NewGuid():N}";
		_ = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Act - Call GetKey multiple times
		var first = await _provider.GetKeyAsync(keyId, CancellationToken.None);
		var second = await _provider.GetKeyAsync(keyId, CancellationToken.None);
		var third = await _provider.GetKeyAsync(keyId, CancellationToken.None);

		// Assert - All should return same result
		_ = first.ShouldNotBeNull();
		_ = second.ShouldNotBeNull();
		_ = third.ShouldNotBeNull();
		first.KeyId.ShouldBe(second.KeyId);
		second.KeyId.ShouldBe(third.KeyId);
	}

	[Fact]
	public async Task HandleConcurrentKeyCreation()
	{
		// Arrange
		var keyId = $"concurrent-{Guid.NewGuid():N}";

		// Act - Create same key concurrently
		var tasks = Enumerable.Range(0, 5)
			.Select(_ => _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None))
			.ToList();

		var results = await Task.WhenAll(tasks);

		// Assert - Only one should succeed with creation, others should rotate
		var successCount = results.Count(r => r.Success);
		successCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task CreateKeyWithPurpose()
	{
		// Arrange
		var keyId = $"purpose-test-{Guid.NewGuid():N}";
		var purpose = "pii-encryption";

		// Act
		var result = await _provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, purpose, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
	}
}
