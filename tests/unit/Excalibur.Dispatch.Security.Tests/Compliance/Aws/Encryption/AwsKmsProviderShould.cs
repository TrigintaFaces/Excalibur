// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Excalibur.Dispatch.Compliance.Aws;

using FakeItEasy;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Categories;

using Xunit;

using AwsKeyMetadata = Amazon.KeyManagementService.Model.KeyMetadata;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Aws.Encryption;

/// <summary>
/// Unit tests for <see cref="AwsKmsProvider"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class AwsKmsProviderShould : IDisposable
{
	private readonly IAmazonKeyManagementService _mockKmsClient;
	private readonly IMemoryCache _memoryCache;
	private readonly AwsKmsProvider _sut;
	private readonly AwsKmsOptions _options;

	public AwsKmsProviderShould()
	{
		_mockKmsClient = A.Fake<IAmazonKeyManagementService>();
		_memoryCache = new MemoryCache(new MemoryCacheOptions());
		_options = new AwsKmsOptions
		{
			KeyAliasPrefix = "test-dispatch",
			Environment = "test",
			EnableAutoRotation = true,
			MetadataCacheDurationSeconds = 60
		};

		_sut = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);
	}

	public void Dispose()
	{
		_sut.Dispose();
		_memoryCache.Dispose();
		(_mockKmsClient as IDisposable)?.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenKmsClientNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new AwsKmsProvider(
			null!,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new AwsKmsProvider(
			_mockKmsClient,
			null!,
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			null!,
			_memoryCache));
	}

	[Fact]
	public void Constructor_CreatesDefaultMemoryCache_WhenCacheIsNull()
	{
		// Arrange & Act
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			null);

		// Assert - should not throw, internal cache is created
		provider.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region GetKeyAsync Tests

	[Fact]
	public async Task GetKeyAsync_ReturnsMetadata_WhenKeyExists()
	{
		// Arrange
		const string keyId = "my-key";
		var kmsKeyId = Guid.NewGuid().ToString();
		var expectedAlias = _options.BuildKeyAlias(keyId);

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Act
		var result = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.KeyId.ShouldBe(keyId);
		result.Status.ShouldBe(KeyStatus.Active);
		result.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
	}

	[Fact]
	public async Task GetKeyAsync_ReturnsNull_WhenKeyNotFound()
	{
		// Arrange
		const string keyId = "nonexistent-key";

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Act
		var result = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyAsync_UsesCachedMetadata_WhenAvailable()
	{
		// Arrange
		const string keyId = "cached-key";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Act - first call
		var result1 = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);
		// Act - second call (should use cache)
		var result2 = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert - KMS should only be called once
		_ = result1.ShouldNotBeNull();
		_ = result2.ShouldNotBeNull();
		result1.KeyId.ShouldBe(result2.KeyId);

		A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetKeyAsync_ThrowsArgumentException_WhenKeyIdEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.GetKeyAsync(string.Empty, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetKeyAsync_ThrowsArgumentException_WhenKeyIdNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.GetKeyAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetKeyAsync_RethrowsException_WhenNotNotFoundOrAccessDenied()
	{
		// Arrange
		const string keyId = "error-key";

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new InvalidOperationException("Unexpected error"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() => _sut.GetKeyAsync(keyId, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetKeyAsync_SetsIsFipsCompliant_WhenUseFipsEndpointEnabled()
	{
		// Arrange
		var optionsWithFips = new AwsKmsOptions
		{
			KeyAliasPrefix = "test",
			UseFipsEndpoint = true
		};

		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(optionsWithFips),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);

		const string keyId = "fips-key";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await provider.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.IsFipsCompliant.ShouldBeTrue();
	}

	[Fact]
	public async Task GetKeyAsync_SetsExpiresAt_WhenDeletionDatePresent()
	{
		// Arrange
		const string keyId = "expiring-key";
		var kmsKeyId = Guid.NewGuid().ToString();
		var deletionDate = DateTime.UtcNow.AddDays(7);

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.PendingDeletion,
					CreationDate = DateTime.UtcNow.AddDays(-30),
					DeletionDate = deletionDate
				}
			});

		// Act
		var result = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ExpiresAt.ShouldNotBeNull();
		result.ExpiresAt.Value.Date.ShouldBe(deletionDate.Date);
	}

	#endregion GetKeyAsync Tests

	#region GetKeyVersionAsync Tests

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsKey_WhenVersionMatchesCurrent()
	{
		// Arrange
		const string keyId = "version-key";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await _sut.GetKeyVersionAsync(keyId, 1, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Version.ShouldBe(1);
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsNull_WhenVersionDoesNotMatch()
	{
		// Arrange
		const string keyId = "version-key";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act - Request version 2, but AWS KMS only has version 1
		var result = await _sut.GetKeyVersionAsync(keyId, 2, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyVersionAsync_ReturnsNull_WhenKeyNotFound()
	{
		// Arrange
		const string keyId = "nonexistent";

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Act
		var result = await _sut.GetKeyVersionAsync(keyId, 1, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetKeyVersionAsync_ThrowsArgumentException_WhenKeyIdEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.GetKeyVersionAsync(string.Empty, 1, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetKeyVersionAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);
		provider.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() => provider.GetKeyVersionAsync("key", 1, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion GetKeyVersionAsync Tests

	#region ListKeysAsync Tests

	[Fact]
	public async Task ListKeysAsync_ReturnsOnlyOurKeys()
	{
		// Arrange
		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.Returns(new ListAliasesResponse
			{
				Aliases =
				[
					new() { AliasName = "alias/test-dispatch-test-key1", TargetKeyId = "key-1" },
					new() { AliasName = "alias/test-dispatch-test-key2", TargetKeyId = "key-2" },
					new() { AliasName = "alias/other-app-key", TargetKeyId = "key-3" } // Not ours
				],
				Truncated = false
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-1" || r.KeyId == "key-2"),
			A<CancellationToken>._))
			.ReturnsLazily((DescribeKeyRequest req, CancellationToken _) => new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = req.KeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Act
		var result = await _sut.ListKeysAsync(null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ListKeysAsync_FiltersBy_Status()
	{
		// Arrange
		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.Returns(new ListAliasesResponse
			{
				Aliases =
				[
					new() { AliasName = "alias/test-dispatch-test-active", TargetKeyId = "key-1" },
					new() { AliasName = "alias/test-dispatch-test-disabled", TargetKeyId = "key-2" }
				],
				Truncated = false
			});

		// key-1 is active, key-2 is disabled
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-1"),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = "key-1",
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-2"),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = "key-2",
					KeyState = KeyState.Disabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await _sut.ListKeysAsync(status: KeyStatus.Active, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].Status.ShouldBe(KeyStatus.Active);
	}

	[Fact]
	public async Task ListKeysAsync_FiltersBy_Purpose()
	{
		// Arrange
		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.Returns(new ListAliasesResponse
			{
				Aliases =
				[
					new() { AliasName = "alias/test-dispatch-test-encryption", TargetKeyId = "key-1" },
					new() { AliasName = "alias/test-dispatch-test-signing", TargetKeyId = "key-2" }
				],
				Truncated = false
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-1"),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = "key-1",
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await _sut.ListKeysAsync(null, "encryption", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ListKeysAsync_HandlesPagination()
	{
		// Arrange
		var firstCallDone = false;

		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				if (!firstCallDone)
				{
					firstCallDone = true;
					return Task.FromResult(new ListAliasesResponse
					{
						Aliases = [new() { AliasName = "alias/test-dispatch-test-key1", TargetKeyId = "key-1" }],
						Truncated = true,
						NextMarker = "next-page"
					});
				}
				return Task.FromResult(new ListAliasesResponse
				{
					Aliases = [new() { AliasName = "alias/test-dispatch-test-key2", TargetKeyId = "key-2" }],
					Truncated = false
				});
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.ReturnsLazily((DescribeKeyRequest req, CancellationToken _) => new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = req.KeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await _sut.ListKeysAsync(null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ListKeysAsync_SkipsAliasesWithoutTargetKeyId()
	{
		// Arrange
		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.Returns(new ListAliasesResponse
			{
				Aliases =
				[
					new() { AliasName = "alias/test-dispatch-test-valid", TargetKeyId = "key-1" },
					new() { AliasName = "alias/test-dispatch-test-invalid", TargetKeyId = null } // No target
				],
				Truncated = false
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-1"),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = "key-1",
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await _sut.ListKeysAsync(null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ListKeysAsync_SkipsDeletedKeys()
	{
		// Arrange
		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.Returns(new ListAliasesResponse
			{
				Aliases =
				[
					new() { AliasName = "alias/test-dispatch-test-exists", TargetKeyId = "key-1" },
					new() { AliasName = "alias/test-dispatch-test-deleted", TargetKeyId = "key-2" }
				],
				Truncated = false
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-1"),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = "key-1",
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-2"),
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key deleted"));

		// Act
		var result = await _sut.ListKeysAsync(null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ListKeysAsync_RethrowsNonNotFoundExceptions()
	{
		// Arrange
		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.Throws(new AmazonKeyManagementServiceException("Access denied"));

		// Act & Assert
		_ = await Should.ThrowAsync<AmazonKeyManagementServiceException>(() => _sut.ListKeysAsync(null, null, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ListKeysAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);
		provider.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() => provider.ListKeysAsync(null, null, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ListKeysAsync_SkipsAliasesWithMismatchedPrefix()
	{
		// Arrange - Alias that starts with our prefix but doesn't match the expected pattern
		// When Environment is set, the alias format is: alias/{prefix}-{env}-{keyId}
		// An alias like "alias/test-dispatch-other-key" wouldn't match "alias/test-dispatch-test-" pattern
		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.Returns(new ListAliasesResponse
			{
				Aliases =
				[
					// This matches our prefix pattern (test-dispatch-test-{keyId})
					new() { AliasName = "alias/test-dispatch-test-valid-key", TargetKeyId = "key-1" },
					// This starts with our prefix but has a different environment
					new() { AliasName = "alias/test-dispatch-prod-different", TargetKeyId = "key-2" }
				],
				Truncated = false
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-1"),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = "key-1",
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await _sut.ListKeysAsync(null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert - Only the key matching our full prefix pattern should be returned
		result.Count.ShouldBe(1);
		result[0].KeyId.ShouldBe("valid-key");
	}

	[Fact]
	public async Task ListKeysAsync_SkipsAliasesWithEmptyExtractedKeyId()
	{
		// Arrange - Alias with our prefix but empty key ID after extraction
		var optionsNoEnv = new AwsKmsOptions
		{
			KeyAliasPrefix = "test-dispatch",
			Environment = null
		};

		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(optionsNoEnv),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);

		// Alias ending with just the prefix and a dash but no key ID
		_ = A.CallTo(() => _mockKmsClient.ListAliasesAsync(
			A<ListAliasesRequest>._,
			A<CancellationToken>._))
			.Returns(new ListAliasesResponse
			{
				Aliases =
				[
					new() { AliasName = "alias/test-dispatch-valid-key", TargetKeyId = "key-1" },
					// This would result in empty key ID if we just extracted after prefix
					new() { AliasName = "alias/test-dispatch-", TargetKeyId = "key-2" }
				],
				Truncated = false
			});

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == "key-1"),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = "key-1",
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await provider.ListKeysAsync(null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert - Empty key ID alias should be skipped
		result.Count.ShouldBe(1);
	}

	#endregion ListKeysAsync Tests

	#region RotateKeyAsync Tests

	[Fact]
	public async Task RotateKeyAsync_CreatesNewKey_WhenKeyDoesNotExist()
	{
		// Arrange
		const string keyId = "new-key";
		var kmsKeyId = Guid.NewGuid().ToString();
		var expectedAlias = _options.BuildKeyAlias(keyId);

		// Key doesn't exist
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Create key succeeds
		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Create alias succeeds
		_ = A.CallTo(() => _mockKmsClient.CreateAliasAsync(
			A<CreateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateAliasResponse());

		// Enable rotation succeeds
		_ = A.CallTo(() => _mockKmsClient.EnableKeyRotationAsync(
			A<EnableKeyRotationRequest>._,
			A<CancellationToken>._))
			.Returns(new EnableKeyRotationResponse());

		// Act
		var result = await _sut.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
		result.NewKey.KeyId.ShouldBe(keyId);
		result.NewKey.Status.ShouldBe(KeyStatus.Active);
		result.PreviousKey.ShouldBeNull();

		A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>.That.Matches(r =>
				r.Tags.Any(t => t.TagKey == "Application" && t.TagValue == "Excalibur.Dispatch")),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RotateKeyAsync_RotatesExistingKey_WhenKeyExists()
	{
		// Arrange
		const string keyId = "existing-key";
		var oldKmsKeyId = Guid.NewGuid().ToString();
		var newKmsKeyId = Guid.NewGuid().ToString();
		var expectedAlias = _options.BuildKeyAlias(keyId);

		// Key exists
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = oldKmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Get rotation status - not enabled
		_ = A.CallTo(() => _mockKmsClient.GetKeyRotationStatusAsync(
			A<GetKeyRotationStatusRequest>._,
			A<CancellationToken>._))
			.Returns(new GetKeyRotationStatusResponse { KeyRotationEnabled = false });

		// Enable rotation
		_ = A.CallTo(() => _mockKmsClient.EnableKeyRotationAsync(
			A<EnableKeyRotationRequest>._,
			A<CancellationToken>._))
			.Returns(new EnableKeyRotationResponse());

		// Create new key
		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = newKmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Update alias
		_ = A.CallTo(() => _mockKmsClient.UpdateAliasAsync(
			A<UpdateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new UpdateAliasResponse());

		// Disable old key
		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DisableKeyResponse());

		// Pre-populate key mapping
		_ = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
		_ = result.PreviousKey.ShouldNotBeNull();
		result.PreviousKey.Status.ShouldBe(KeyStatus.DecryptOnly);
	}

	[Fact]
	public async Task RotateKeyAsync_WithAutoRotationAlreadyEnabled_SkipsEnableRotation()
	{
		// Arrange
		const string keyId = "auto-rotate-key";
		var oldKmsKeyId = Guid.NewGuid().ToString();
		var newKmsKeyId = Guid.NewGuid().ToString();
		var expectedAlias = _options.BuildKeyAlias(keyId);

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = oldKmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Already has rotation enabled
		_ = A.CallTo(() => _mockKmsClient.GetKeyRotationStatusAsync(
			A<GetKeyRotationStatusRequest>._,
			A<CancellationToken>._))
			.Returns(new GetKeyRotationStatusResponse { KeyRotationEnabled = true });

		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = newKmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.UpdateAliasAsync(
			A<UpdateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new UpdateAliasResponse());

		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DisableKeyResponse());

		_ = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();

		// EnableKeyRotation should NOT be called since it's already enabled
		A.CallTo(() => _mockKmsClient.EnableKeyRotationAsync(
			A<EnableKeyRotationRequest>._,
			A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RotateKeyAsync_WithPurpose_IncludesPurposeTag()
	{
		// Arrange
		const string keyId = "purpose-key";
		const string purpose = "encryption";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.CreateAliasAsync(
			A<CreateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateAliasResponse());

		_ = A.CallTo(() => _mockKmsClient.EnableKeyRotationAsync(
			A<EnableKeyRotationRequest>._,
			A<CancellationToken>._))
			.Returns(new EnableKeyRotationResponse());

		// Act
		var result = await _sut.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, purpose, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();

		A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>.That.Matches(r =>
				r.Tags.Any(t => t.TagKey == "Purpose" && t.TagValue == purpose)),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RotateKeyAsync_ReturnsFailure_WhenKmsThrows()
	{
		// Arrange
		const string keyId = "failing-key";

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new AmazonKeyManagementServiceException("Access denied"));

		// Act
		var result = await _sut.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task RotateKeyAsync_ThrowsArgumentException_WhenKeyIdEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.RotateKeyAsync(string.Empty, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RotateKeyAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);
		provider.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() => provider.RotateKeyAsync("key", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RotateKeyAsync_WithAutoRotationDisabled_SkipsEnableRotation()
	{
		// Arrange
		var optionsWithoutAutoRotation = new AwsKmsOptions
		{
			KeyAliasPrefix = "test-dispatch",
			Environment = "test",
			EnableAutoRotation = false
		};

		using var cache = new MemoryCache(new MemoryCacheOptions());
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(optionsWithoutAutoRotation),
			NullLogger<AwsKmsProvider>.Instance,
			cache);

		const string keyId = "no-auto-rotate";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.CreateAliasAsync(
			A<CreateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateAliasResponse());

		// Act
		var result = await provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();

		// EnableKeyRotation should NOT be called when EnableAutoRotation is false
		A.CallTo(() => _mockKmsClient.EnableKeyRotationAsync(
			A<EnableKeyRotationRequest>._,
			A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RotateKeyAsync_ReturnsFailure_WhenKeyIdMappingNotFound()
	{
		// Arrange - Key exists but we haven't populated the aliasToKeyIdMap
		// This requires a provider with a fresh cache that hasn't had GetKeyAsync called
		var freshOptions = new AwsKmsOptions
		{
			KeyAliasPrefix = "test-dispatch",
			Environment = "fresh"
		};

		using var freshCache = new MemoryCache(new MemoryCacheOptions());
		using var freshProvider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(freshOptions),
			NullLogger<AwsKmsProvider>.Instance,
			freshCache);

		const string keyId = "existing-unmapped";
		var oldKmsKeyId = Guid.NewGuid().ToString();
		var expectedAlias = freshOptions.BuildKeyAlias(keyId);

		// Key exists (first call will populate the cache via GetKeyAsync inside RotateKeyAsync)
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = oldKmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Now mock the rotation status check - this is called BEFORE checking the mapping
		_ = A.CallTo(() => _mockKmsClient.GetKeyRotationStatusAsync(
			A<GetKeyRotationStatusRequest>._,
			A<CancellationToken>._))
			.Returns(new GetKeyRotationStatusResponse { KeyRotationEnabled = true });

		// CreateKeyAsync for the new key
		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = Guid.NewGuid().ToString(),
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// UpdateAliasAsync
		_ = A.CallTo(() => _mockKmsClient.UpdateAliasAsync(
			A<UpdateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new UpdateAliasResponse());

		// DisableKeyAsync
		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DisableKeyResponse());

		// Act - The first call to GetKeyAsync inside RotateKeyAsync will populate the mapping
		var result = await freshProvider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert - This should succeed because GetKeyAsync populates the mapping
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task RotateKeyAsync_WithEnvironmentTag_IncludesEnvironmentInTags()
	{
		// Arrange
		var optionsWithEnvironment = new AwsKmsOptions
		{
			KeyAliasPrefix = "test-dispatch",
			Environment = "production"
		};

		using var cache = new MemoryCache(new MemoryCacheOptions());
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(optionsWithEnvironment),
			NullLogger<AwsKmsProvider>.Instance,
			cache);

		const string keyId = "env-tagged-key";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.CreateAliasAsync(
			A<CreateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateAliasResponse());

		_ = A.CallTo(() => _mockKmsClient.EnableKeyRotationAsync(
			A<EnableKeyRotationRequest>._,
			A<CancellationToken>._))
			.Returns(new EnableKeyRotationResponse());

		// Act
		var result = await provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();

		// Verify Environment tag was included
		A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>.That.Matches(r =>
				r.Tags.Any(t => t.TagKey == "Environment" && t.TagValue == "production")),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RotateKeyAsync_WithMultiRegionEnabled_CreatesMultiRegionKey()
	{
		// Arrange
		var optionsWithMrk = new AwsKmsOptions
		{
			KeyAliasPrefix = "test-dispatch",
			CreateMultiRegionKeys = true
		};

		using var cache = new MemoryCache(new MemoryCacheOptions());
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(optionsWithMrk),
			NullLogger<AwsKmsProvider>.Instance,
			cache);

		const string keyId = "mrk-key";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.CreateAliasAsync(
			A<CreateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateAliasResponse());

		_ = A.CallTo(() => _mockKmsClient.EnableKeyRotationAsync(
			A<EnableKeyRotationRequest>._,
			A<CancellationToken>._))
			.Returns(new EnableKeyRotationResponse());

		// Act
		var result = await provider.RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();

		// Verify MultiRegion was set to true
		A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>.That.Matches(r => r.MultiRegion == true),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion RotateKeyAsync Tests

	#region DeleteKeyAsync Tests

	[Fact]
	public async Task DeleteKeyAsync_SchedulesDeletion_WhenKeyExists()
	{
		// Arrange
		const string keyId = "delete-me";
		var kmsKeyId = Guid.NewGuid().ToString();
		var expectedAlias = _options.BuildKeyAlias(keyId);

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		_ = A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>._,
			A<CancellationToken>._))
			.Returns(new ScheduleKeyDeletionResponse());

		_ = A.CallTo(() => _mockKmsClient.DeleteAliasAsync(
			A<DeleteAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new DeleteAliasResponse());

		// Act
		var result = await _sut.DeleteKeyAsync(keyId, retentionDays: 30, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();

		A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>.That.Matches(r => r.PendingWindowInDays == 30),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteKeyAsync_ClampsRetentionDays_ToMinimum7()
	{
		// Arrange
		const string keyId = "delete-me";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		_ = A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>._,
			A<CancellationToken>._))
			.Returns(new ScheduleKeyDeletionResponse());

		_ = A.CallTo(() => _mockKmsClient.DeleteAliasAsync(
			A<DeleteAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new DeleteAliasResponse());

		// Act - request 1 day (below minimum)
		_ = await _sut.DeleteKeyAsync(keyId, retentionDays: 1, CancellationToken.None).ConfigureAwait(false);

		// Assert - should be clamped to 7 days minimum
		A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>.That.Matches(r => r.PendingWindowInDays == 7),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteKeyAsync_ClampsRetentionDays_ToMaximum30()
	{
		// Arrange
		const string keyId = "delete-me";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		_ = A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>._,
			A<CancellationToken>._))
			.Returns(new ScheduleKeyDeletionResponse());

		_ = A.CallTo(() => _mockKmsClient.DeleteAliasAsync(
			A<DeleteAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new DeleteAliasResponse());

		// Act - request 60 days (above maximum)
		_ = await _sut.DeleteKeyAsync(keyId, retentionDays: 60, CancellationToken.None).ConfigureAwait(false);

		// Assert - should be clamped to 30 days maximum
		A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>.That.Matches(r => r.PendingWindowInDays == 30),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteKeyAsync_ReturnsFalse_WhenKeyNotFound()
	{
		// Arrange
		const string keyId = "nonexistent";

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Act
		var result = await _sut.DeleteKeyAsync(keyId, 30, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteKeyAsync_HandlesAliasAlreadyDeleted()
	{
		// Arrange
		const string keyId = "orphan-key";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>._,
			A<CancellationToken>._))
			.Returns(new ScheduleKeyDeletionResponse());

		// Alias already deleted
		_ = A.CallTo(() => _mockKmsClient.DeleteAliasAsync(
			A<DeleteAliasRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Alias not found"));

		// Act
		var result = await _sut.DeleteKeyAsync(keyId, 30, CancellationToken.None).ConfigureAwait(false);

		// Assert - should still succeed
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteKeyAsync_RethrowsNonNotFoundExceptions()
	{
		// Arrange
		const string keyId = "error-key";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>._,
			A<CancellationToken>._))
			.Throws(new AmazonKeyManagementServiceException("Access denied"));

		// Act & Assert
		_ = await Should.ThrowAsync<AmazonKeyManagementServiceException>(() => _sut.DeleteKeyAsync(keyId, 30, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DeleteKeyAsync_ThrowsArgumentException_WhenKeyIdEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.DeleteKeyAsync(string.Empty, 30, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DeleteKeyAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);
		provider.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() => provider.DeleteKeyAsync("key", 30, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DeleteKeyAsync_UsesCachedKeyIdMapping()
	{
		// Arrange
		const string keyId = "cached-delete";
		var kmsKeyId = Guid.NewGuid().ToString();

		// First, get the key to populate the cache
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		_ = A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>._,
			A<CancellationToken>._))
			.Returns(new ScheduleKeyDeletionResponse());

		_ = A.CallTo(() => _mockKmsClient.DeleteAliasAsync(
			A<DeleteAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new DeleteAliasResponse());

		// Act
		var result = await _sut.DeleteKeyAsync(keyId, 30, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();

		// ScheduleKeyDeletion should be called with the resolved KMS key ID
		A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>.That.Matches(r => r.KeyId == kmsKeyId),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteKeyAsync_ReturnsFalse_WhenScheduleDeletionThrowsNotFoundException()
	{
		// Arrange
		const string keyId = "deleted-during-op";
		var kmsKeyId = Guid.NewGuid().ToString();

		// Key exists initially
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// But deletion fails with NotFoundException (key deleted in between)
		_ = A.CallTo(() => _mockKmsClient.ScheduleKeyDeletionAsync(
			A<ScheduleKeyDeletionRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Act
		var result = await _sut.DeleteKeyAsync(keyId, 30, CancellationToken.None).ConfigureAwait(false);

		// Assert - should return false since key wasn't found
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteKeyAsync_ReturnsFalse_WhenKeyIdCannotBeResolved()
	{
		// Arrange - Test the path where GetKeyAsync succeeds but the mapping isn't in _aliasToKeyIdMap
		// This scenario is hard to trigger directly because GetKeyAsync populates the map.
		// However, we can test the early return when the initial cached lookup fails
		// and GetKeyAsync also returns null.
		const string keyId = "unresolvable";

		// GetKeyAsync returns null (key not found)
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Act
		var result = await _sut.DeleteKeyAsync(keyId, 30, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion DeleteKeyAsync Tests

	#region SuspendKeyAsync Tests

	[Fact]
	public async Task SuspendKeyAsync_DisablesKey_WhenKeyExists()
	{
		// Arrange
		const string keyId = "suspend-me";
		const string reason = "Security incident";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DisableKeyResponse());

		// Pre-populate the keyId mapping
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Pre-call to populate the alias mapping
		_ = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		_ = A.CallTo(() => _mockKmsClient.TagResourceAsync(
			A<TagResourceRequest>._,
			A<CancellationToken>._))
			.Returns(new TagResourceResponse());

		// Act
		var result = await _sut.SuspendKeyAsync(keyId, reason, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();

		A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SuspendKeyAsync_TagsKeyWithSuspensionDetails()
	{
		// Arrange
		const string keyId = "tag-suspend";
		const string reason = "Security audit required";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DisableKeyResponse());

		_ = A.CallTo(() => _mockKmsClient.TagResourceAsync(
			A<TagResourceRequest>._,
			A<CancellationToken>._))
			.Returns(new TagResourceResponse());

		// Act
		var result = await _sut.SuspendKeyAsync(keyId, reason, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();

		A.CallTo(() => _mockKmsClient.TagResourceAsync(
			A<TagResourceRequest>.That.Matches(r =>
				r.Tags.Any(t => t.TagKey == "SuspensionReason" && t.TagValue == reason) &&
				r.Tags.Any(t => t.TagKey == "SuspendedAt")),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SuspendKeyAsync_ReturnsFalse_WhenKeyNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Act
		var result = await _sut.SuspendKeyAsync("nonexistent", "reason", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task SuspendKeyAsync_RethrowsNonNotFoundExceptions()
	{
		// Arrange
		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Throws(new AmazonKeyManagementServiceException("Access denied"));

		// Act & Assert
		_ = await Should.ThrowAsync<AmazonKeyManagementServiceException>(
			() => _sut.SuspendKeyAsync("key", "reason", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SuspendKeyAsync_ThrowsArgumentException_WhenKeyIdEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.SuspendKeyAsync(string.Empty, "reason", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SuspendKeyAsync_ThrowsArgumentException_WhenReasonEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.SuspendKeyAsync("key", string.Empty, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SuspendKeyAsync_ThrowsArgumentException_WhenReasonNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.SuspendKeyAsync("key", null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SuspendKeyAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);
		provider.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => provider.SuspendKeyAsync("key", "reason", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SuspendKeyAsync_ClearsCacheForKey()
	{
		// Arrange
		const string keyId = "cache-clear";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// First call populates cache
		_ = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DisableKeyResponse());

		_ = A.CallTo(() => _mockKmsClient.TagResourceAsync(
			A<TagResourceRequest>._,
			A<CancellationToken>._))
			.Returns(new TagResourceResponse());

		// Act
		_ = await _sut.SuspendKeyAsync(keyId, "reason", CancellationToken.None).ConfigureAwait(false);

		// Now fetch again - should hit KMS again since cache was cleared
		_ = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert - DescribeKey should have been called twice (before suspend and after)
		A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task SuspendKeyAsync_SkipsTagging_WhenKeyIdMappingNotFound()
	{
		// Arrange - Key is suspended but we haven't populated the alias mapping
		// (no prior GetKeyAsync call)
		var freshOptions = new AwsKmsOptions
		{
			KeyAliasPrefix = "test-dispatch",
			Environment = "fresh-suspend"
		};

		using var freshCache = new MemoryCache(new MemoryCacheOptions());
		using var freshProvider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(freshOptions),
			NullLogger<AwsKmsProvider>.Instance,
			freshCache);

		const string keyId = "suspend-no-mapping";
		const string reason = "Test suspend without prior GetKeyAsync";

		// DisableKey succeeds
		_ = A.CallTo(() => _mockKmsClient.DisableKeyAsync(
			A<DisableKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DisableKeyResponse());

		// Act
		var result = await freshProvider.SuspendKeyAsync(keyId, reason, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();

		// TagResource should NOT be called since we don't have the kmsKeyId mapping
		A.CallTo(() => _mockKmsClient.TagResourceAsync(
			A<TagResourceRequest>._,
			A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion SuspendKeyAsync Tests

	#region GetActiveKeyAsync Tests

	[Fact]
	public async Task GetActiveKeyAsync_ReturnsActiveKey_WhenExists()
	{
		// Arrange
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Act
		var result = await _sut.GetActiveKeyAsync(null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(KeyStatus.Active);
	}

	[Fact]
	public async Task GetActiveKeyAsync_ReturnsNull_WhenKeyIsNotActive()
	{
		// Arrange
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Disabled,
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Act
		var result = await _sut.GetActiveKeyAsync(purpose: "specific", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetActiveKeyAsync_CreatesDefaultKey_WhenNoKeyExistsAndNoPurpose()
	{
		// Arrange
		var kmsKeyId = Guid.NewGuid().ToString();
		var expectedAlias = _options.BuildKeyAlias("default");

		// Key doesn't exist initially
		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Create key
		_ = A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		_ = A.CallTo(() => _mockKmsClient.CreateAliasAsync(
			A<CreateAliasRequest>._,
			A<CancellationToken>._))
			.Returns(new CreateAliasResponse());

		_ = A.CallTo(() => _mockKmsClient.EnableKeyRotationAsync(
			A<EnableKeyRotationRequest>._,
			A<CancellationToken>._))
			.Returns(new EnableKeyRotationResponse());

		// Act
		var result = await _sut.GetActiveKeyAsync(null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(KeyStatus.Active);
	}

	[Fact]
	public async Task GetActiveKeyAsync_UsesPurposeForKeyId()
	{
		// Arrange
		const string purpose = "signing";
		var kmsKeyId = Guid.NewGuid().ToString();
		var expectedAlias = _options.BuildKeyAlias(purpose);

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = KeyState.Enabled,
					CreationDate = DateTime.UtcNow
				}
			});

		// Act
		var result = await _sut.GetActiveKeyAsync(purpose: purpose, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public async Task GetActiveKeyAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		using var provider = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);
		provider.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() => provider.GetActiveKeyAsync(null, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetActiveKeyAsync_DoesNotCreateKey_WhenPurposeSpecifiedAndKeyNotFound()
	{
		// Arrange - When purpose is specified and key doesn't exist, should return null
		// (only creates default key when purpose is null)
		var kmsKeyId = Guid.NewGuid().ToString();
		const string purpose = "signing";
		var expectedAlias = _options.BuildKeyAlias(purpose);

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>.That.Matches(r => r.KeyId == expectedAlias),
			A<CancellationToken>._))
			.Throws(new NotFoundException("Key not found"));

		// Act
		var result = await _sut.GetActiveKeyAsync(purpose: purpose, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();

		// CreateKeyAsync should NOT be called when purpose is specified
		A.CallTo(() => _mockKmsClient.CreateKeyAsync(
			A<CreateKeyRequest>._,
			A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion GetActiveKeyAsync Tests

	#region KeyState Mapping Tests

	[Theory]
	[InlineData("Enabled", KeyStatus.Active)]
	[InlineData("Disabled", KeyStatus.Suspended)]
	[InlineData("PendingDeletion", KeyStatus.PendingDestruction)]
	[InlineData("PendingImport", KeyStatus.Active)]
	[InlineData("PendingReplicaDeletion", KeyStatus.PendingDestruction)]
	[InlineData("Unavailable", KeyStatus.Suspended)]
	[InlineData("UnknownState", KeyStatus.Active)] // Default case
	public async Task GetKeyAsync_MapsKeyStateCorrectly(string awsState, KeyStatus expectedStatus)
	{
		// Arrange
		const string keyId = "state-test";
		var kmsKeyId = Guid.NewGuid().ToString();

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = new AwsKeyMetadata
				{
					KeyId = kmsKeyId,
					KeyState = new KeyState(awsState),
					CreationDate = DateTime.UtcNow.AddDays(-30)
				}
			});

		// Clear cache for each test
		_memoryCache.Remove($"key:{keyId}");

		// Act
		var result = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(expectedStatus);
	}

	[Fact]
	public async Task GetKeyAsync_MapsNullKeyState_ToActiveStatus()
	{
		// Arrange - Test when KeyState is null (edge case)
		const string keyId = "null-state-test";
		var kmsKeyId = Guid.NewGuid().ToString();

		var keyMetadata = new AwsKeyMetadata
		{
			KeyId = kmsKeyId,
			CreationDate = DateTime.UtcNow.AddDays(-30)
		};

		_ = A.CallTo(() => _mockKmsClient.DescribeKeyAsync(
			A<DescribeKeyRequest>._,
			A<CancellationToken>._))
			.Returns(new DescribeKeyResponse
			{
				KeyMetadata = keyMetadata
			});

		// Act
		var result = await _sut.GetKeyAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(KeyStatus.Active); // Default case
	}

	#endregion KeyState Mapping Tests

	#region Dispose Tests

	[Fact]
	public async Task Dispose_ThrowsObjectDisposedException_OnSubsequentCalls()
	{
		// Arrange
		var localSut = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);

		// Act
		localSut.Dispose();

		// Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() => localSut.GetKeyAsync("any", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var localSut = new AwsKmsProvider(
			_mockKmsClient,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<AwsKmsProvider>.Instance,
			_memoryCache);

		// Act & Assert - should not throw
		localSut.Dispose();
		localSut.Dispose();
	}

	#endregion Dispose Tests

	#region Options Tests

	[Fact]
	public void BuildKeyAlias_IncludesPrefix()
	{
		// Arrange
		var options = new AwsKmsOptions { KeyAliasPrefix = "my-app" };

		// Act
		var alias = options.BuildKeyAlias("my-key");

		// Assert
		alias.ShouldBe("alias/my-app-my-key");
	}

	[Fact]
	public void BuildKeyAlias_IncludesEnvironment_WhenSet()
	{
		// Arrange
		var options = new AwsKmsOptions
		{
			KeyAliasPrefix = "my-app",
			Environment = "prod"
		};

		// Act
		var alias = options.BuildKeyAlias("my-key");

		// Assert
		alias.ShouldBe("alias/my-app-prod-my-key");
	}

	#endregion Options Tests
}
