// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.KeyRotation;

[Trait("Category", TestCategories.Unit)]
public sealed class KeyRotationServiceShould : IDisposable
{
	private readonly IKeyManagementProvider _keyProvider;
	private readonly IOptions<KeyRotationOptions> _options;
	private readonly ILogger<KeyRotationService> _logger;
	private KeyRotationService? _sut;

	public KeyRotationServiceShould()
	{
		_keyProvider = A.Fake<IKeyManagementProvider>();
		_options = Microsoft.Extensions.Options.Options.Create(new KeyRotationOptions
		{
			Enabled = true,
			CheckInterval = TimeSpan.FromMilliseconds(100),
			MaxConcurrentRotations = 2,
			ContinueOnError = true
		});
		_logger = NullLogger<KeyRotationService>.Instance;
	}

	public void Dispose()
	{
		_sut?.Dispose();
	}

	[Fact]
	public void ThrowWhenKeyProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new KeyRotationService(null!, _options, _logger));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new KeyRotationService(_keyProvider, null!, _logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new KeyRotationService(_keyProvider, _options, null!));
	}

	[Fact]
	public async Task CheckAndRotateAsync_ReturnEmptyResult_WhenNoActiveKeys()
	{
		// Arrange
		_ = A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.CheckAndRotateAsync(CancellationToken.None);

		// Assert
		result.KeysChecked.ShouldBe(0);
		result.KeysRotated.ShouldBe(0);
		result.KeysFailed.ShouldBe(0);
		result.AllSucceeded.ShouldBeTrue();
	}

	[Fact]
	public async Task CheckAndRotateAsync_RotateKeysDueForRotation()
	{
		// Arrange
		var oldKey = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-100), // Older than 90 days
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		var newKey = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 2,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow,
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		_ = A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([oldKey]));

		_ = A.CallTo(() => _keyProvider.RotateKeyAsync(
				"key-1",
				A<EncryptionAlgorithm>._,
				A<string?>._,
				A<DateTimeOffset?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(KeyRotationResult.Succeeded(newKey)));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.CheckAndRotateAsync(CancellationToken.None);

		// Assert
		result.KeysChecked.ShouldBe(1);
		result.KeysDueForRotation.ShouldBe(1);
		result.KeysRotated.ShouldBe(1);
		result.KeysFailed.ShouldBe(0);
		result.AllSucceeded.ShouldBeTrue();
	}

	[Fact]
	public async Task CheckAndRotateAsync_SkipKeysNotDueForRotation()
	{
		// Arrange
		var recentKey = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-10), // Only 10 days old
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		_ = A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([recentKey]));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.CheckAndRotateAsync(CancellationToken.None);

		// Assert
		result.KeysChecked.ShouldBe(1);
		result.KeysDueForRotation.ShouldBe(0);
		result.KeysRotated.ShouldBe(0);

		// Verify rotation was never called
		A.CallTo(() => _keyProvider.RotateKeyAsync(
				A<string>._,
				A<EncryptionAlgorithm>._,
				A<string?>._,
				A<DateTimeOffset?>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task CheckAndRotateAsync_ContinueOnError_WhenEnabled()
	{
		// Arrange
		var keys = new List<KeyMetadata>
		{
			new()
			{
				KeyId = "key-1",
				Version = 1,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
				Algorithm = EncryptionAlgorithm.Aes256Gcm
			},
			new()
			{
				KeyId = "key-2",
				Version = 1,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
				Algorithm = EncryptionAlgorithm.Aes256Gcm
			}
		};

		_ = A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>(keys));

		// First key fails
		_ = A.CallTo(() => _keyProvider.RotateKeyAsync(
				"key-1",
				A<EncryptionAlgorithm>._,
				A<string?>._,
				A<DateTimeOffset?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(KeyRotationResult.Failed("Rotation failed")));

		// Second key succeeds
		_ = A.CallTo(() => _keyProvider.RotateKeyAsync(
				"key-2",
				A<EncryptionAlgorithm>._,
				A<string?>._,
				A<DateTimeOffset?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(KeyRotationResult.Succeeded(new KeyMetadata
			{
				KeyId = "key-2",
				Version = 2,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UtcNow,
				Algorithm = EncryptionAlgorithm.Aes256Gcm
			})));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.CheckAndRotateAsync(CancellationToken.None);

		// Assert
		result.KeysChecked.ShouldBe(2);
		result.KeysDueForRotation.ShouldBe(2);
		result.KeysRotated.ShouldBe(1);
		result.KeysFailed.ShouldBe(1);
		result.AllSucceeded.ShouldBeFalse();
	}

	[Fact]
	public async Task IsRotationDueAsync_ReturnFalse_WhenKeyNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _keyProvider.GetKeyAsync("non-existent", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.IsRotationDueAsync("non-existent", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsRotationDueAsync_ReturnTrue_WhenKeyIsDueForRotation()
	{
		// Arrange
		var oldKey = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(oldKey));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.IsRotationDueAsync("key-1", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task IsRotationDueAsync_ThrowOnNullKeyId()
	{
		// Arrange
		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.IsRotationDueAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task IsRotationDueAsync_ThrowOnEmptyKeyId()
	{
		// Arrange
		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.IsRotationDueAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task ForceRotateAsync_RotateKey_WhenKeyExists()
	{
		// Arrange
		var existingKey = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-10), // Recent key
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		var newKey = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 2,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow,
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(existingKey));

		_ = A.CallTo(() => _keyProvider.RotateKeyAsync(
				"key-1",
				A<EncryptionAlgorithm>._,
				A<string?>._,
				A<DateTimeOffset?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(KeyRotationResult.Succeeded(newKey)));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.ForceRotateAsync("key-1", "Security incident", CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
		result.NewKey.Version.ShouldBe(2);
	}

	[Fact]
	public async Task ForceRotateAsync_ReturnFailed_WhenKeyNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _keyProvider.GetKeyAsync("non-existent", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.ForceRotateAsync("non-existent", "Testing", CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("not found");
	}

	[Fact]
	public async Task ForceRotateAsync_ThrowOnNullKeyId()
	{
		// Arrange
		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.ForceRotateAsync(null!, "reason", CancellationToken.None));
	}

	[Fact]
	public async Task ForceRotateAsync_ThrowOnNullReason()
	{
		// Arrange
		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _sut.ForceRotateAsync("key-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetNextRotationTimeAsync_ReturnNull_WhenKeyNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _keyProvider.GetKeyAsync("non-existent", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.GetNextRotationTimeAsync("non-existent", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetNextRotationTimeAsync_ReturnNextRotationTime_WhenAutoRotateEnabled()
	{
		// Arrange
		var key = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(key));

		_sut = new KeyRotationService(_keyProvider, _options, _logger);

		// Act
		var result = await _sut.GetNextRotationTimeAsync("key-1", CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBeGreaterThan(key.CreatedAt);
	}

	[Fact]
	public async Task GetNextRotationTimeAsync_ReturnNull_WhenAutoRotateDisabled()
	{
		// Arrange
		var key = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(key));

		var disabledOptions = Microsoft.Extensions.Options.Options.Create(new KeyRotationOptions
		{
			Enabled = true,
			DefaultPolicy = new KeyRotationPolicy
			{
				Name = "NoAutoRotate",
				AutoRotateEnabled = false
			}
		});

		_sut = new KeyRotationService(_keyProvider, disabledOptions, _logger);

		// Act
		var result = await _sut.GetNextRotationTimeAsync("key-1", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task CheckAndRotateAsync_UsePurposeSpecificPolicy()
	{
		// Arrange
		var key = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-35), // 35 days old
			Purpose = "high-security",
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		var options = Microsoft.Extensions.Options.Options.Create(new KeyRotationOptions
		{
			Enabled = true,
			CheckInterval = TimeSpan.FromMilliseconds(100),
			DefaultPolicy = KeyRotationPolicy.Default // 90 days
		}.AddHighSecurityPolicy("high-security")); // 30 days

		_ = A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([key]));

		_ = A.CallTo(() => _keyProvider.RotateKeyAsync(
				"key-1",
				A<EncryptionAlgorithm>._,
				A<string?>._,
				A<DateTimeOffset?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(KeyRotationResult.Succeeded(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 2,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UtcNow,
				Purpose = "high-security",
				Algorithm = EncryptionAlgorithm.Aes256Gcm
			})));

		_sut = new KeyRotationService(_keyProvider, options, _logger);

		// Act
		var result = await _sut.CheckAndRotateAsync(CancellationToken.None);

		// Assert - should rotate because high-security policy has 30-day max age
		result.KeysDueForRotation.ShouldBe(1);
		result.KeysRotated.ShouldBe(1);
	}
}
