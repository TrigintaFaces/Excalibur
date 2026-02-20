// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

[Trait("Category", TestCategories.Unit)]
public sealed class MultiRegionKeyProviderShould : IDisposable
{
	private readonly IKeyManagementProvider _primaryProvider;
	private readonly IKeyManagementProvider _secondaryProvider;
	private readonly ILogger<MultiRegionKeyProvider> _logger;
	private readonly MultiRegionOptions _options;
	private MultiRegionKeyProvider? _sut;

	public MultiRegionKeyProviderShould()
	{
		_primaryProvider = A.Fake<IKeyManagementProvider>();
		_secondaryProvider = A.Fake<IKeyManagementProvider>();
		_logger = NullLogger<MultiRegionKeyProvider>.Instance;
		_options = CreateDefaultOptions();

		// Setup default healthy providers
		_ = A.CallTo(() => _primaryProvider.ListKeysAsync(
				A<KeyStatus?>._,
				A<string?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));

		_ = A.CallTo(() => _secondaryProvider.ListKeysAsync(
				A<KeyStatus?>._,
				A<string?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));
	}

	public void Dispose()
	{
		_sut?.Dispose();
	}

	[Fact]
	public void ThrowWhenPrimaryProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MultiRegionKeyProvider(null!, _secondaryProvider, _options, _logger));
	}

	[Fact]
	public void ThrowWhenSecondaryProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MultiRegionKeyProvider(_primaryProvider, null!, _options, _logger));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MultiRegionKeyProvider(_primaryProvider, _secondaryProvider, null!, _logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MultiRegionKeyProvider(_primaryProvider, _secondaryProvider, _options, null!));
	}

	[Fact]
	public void ExposeActiveRegionId_AsPrimary_Initially()
	{
		// Arrange
		var sut = CreateSut();

		// Assert
		sut.ActiveRegionId.ShouldBe("primary-region");
	}

	[Fact]
	public void ExposeIsInFailoverMode_AsFalse_Initially()
	{
		// Arrange
		var sut = CreateSut();

		// Assert
		sut.IsInFailoverMode.ShouldBeFalse();
	}

	[Fact]
	public async Task GetPrimaryHealthAsync_ReturnHealthyStatus()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var health = await sut.GetPrimaryHealthAsync(CancellationToken.None);

		// Assert
		_ = health.ShouldNotBeNull();
		health.RegionId.ShouldBe("primary-region");
		health.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public async Task GetSecondaryHealthAsync_ReturnHealthyStatus()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var health = await sut.GetSecondaryHealthAsync(CancellationToken.None);

		// Assert
		_ = health.ShouldNotBeNull();
		health.RegionId.ShouldBe("secondary-region");
		health.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public async Task GetPrimaryHealthAsync_ReturnUnhealthyStatus_WhenProviderFails()
	{
		// Arrange
		_ = A.CallTo(() => _primaryProvider.ListKeysAsync(
				A<KeyStatus?>._,
				A<string?>._,
				A<CancellationToken>._))
			.Throws(new InvalidOperationException("Connection failed"));

		var sut = CreateSut();

		// Act
		var health = await sut.GetPrimaryHealthAsync(CancellationToken.None);

		// Assert
		health.IsHealthy.ShouldBeFalse();
		health.ErrorMessage.ShouldContain("Connection failed");
	}

	[Fact]
	public async Task GetReplicationStatusAsync_ReturnStatus()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var status = await sut.GetReplicationStatusAsync(CancellationToken.None);

		// Assert
		_ = status.ShouldNotBeNull();
		status.ReplicationMode.ShouldBe(ReplicationMode.Asynchronous);
		status.SyncInProgress.ShouldBeFalse();
		status.PendingKeys.ShouldBe(0);
	}

	[Fact]
	public async Task ForceFailoverAsync_ThrowOnNullReason()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			sut.ForceFailoverAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ForceFailoverAsync_ThrowOnEmptyReason()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			sut.ForceFailoverAsync(string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task ForceFailoverAsync_SwitchToSecondaryRegion()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		await sut.ForceFailoverAsync("Manual maintenance", CancellationToken.None);

		// Assert
		sut.IsInFailoverMode.ShouldBeTrue();
		sut.ActiveRegionId.ShouldBe("secondary-region");
	}

	[Fact]
	public async Task ForceFailoverAsync_ThrowWhenAlreadyInFailoverMode()
	{
		// Arrange
		var sut = CreateSut();
		await sut.ForceFailoverAsync("First failover", CancellationToken.None);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.ForceFailoverAsync("Second failover", CancellationToken.None));

		ex.Message.ShouldContain("Already in failover mode");
	}

	[Fact]
	public async Task ForceFailoverAsync_ThrowWhenSecondaryUnhealthy()
	{
		// Arrange
		_ = A.CallTo(() => _secondaryProvider.ListKeysAsync(
				A<KeyStatus?>._,
				A<string?>._,
				A<CancellationToken>._))
			.Throws(new InvalidOperationException("Secondary down"));

		var sut = CreateSut();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.ForceFailoverAsync("Failover attempt", CancellationToken.None));

		ex.Message.ShouldContain("Cannot failover");
		ex.Message.ShouldContain("unhealthy");
	}

	[Fact]
	public async Task FailbackToPrimaryAsync_ThrowOnNullReason()
	{
		// Arrange
		var sut = CreateSut();
		await sut.ForceFailoverAsync("Setup failover", CancellationToken.None);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			sut.FailbackToPrimaryAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task FailbackToPrimaryAsync_ThrowWhenNotInFailoverMode()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.FailbackToPrimaryAsync("Failback attempt", CancellationToken.None));

		ex.Message.ShouldContain("Not in failover mode");
	}

	[Fact]
	public async Task FailbackToPrimaryAsync_ReturnToPrimaryRegion()
	{
		// Arrange
		var sut = CreateSut();
		await sut.ForceFailoverAsync("Manual failover", CancellationToken.None);
		sut.IsInFailoverMode.ShouldBeTrue();

		// Act
		await sut.FailbackToPrimaryAsync("Primary recovered", CancellationToken.None);

		// Assert
		sut.IsInFailoverMode.ShouldBeFalse();
		sut.ActiveRegionId.ShouldBe("primary-region");
	}

	[Fact]
	public async Task FailbackToPrimaryAsync_ThrowWhenPrimaryUnhealthy()
	{
		// Arrange
		var sut = CreateSut();
		await sut.ForceFailoverAsync("Setup failover", CancellationToken.None);

		_ = A.CallTo(() => _primaryProvider.ListKeysAsync(
				A<KeyStatus?>._,
				A<string?>._,
				A<CancellationToken>._))
			.Throws(new InvalidOperationException("Primary still down"));

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.FailbackToPrimaryAsync("Premature failback", CancellationToken.None));

		ex.Message.ShouldContain("Cannot failback");
		ex.Message.ShouldContain("unhealthy");
	}

	[Fact]
	public async Task ReplicateKeysAsync_CompleteWithoutError()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert - should not throw
		await sut.ReplicateKeysAsync(null, CancellationToken.None);
	}

	[Fact]
	public async Task ReplicateKeysAsync_AcceptSpecificKeyId()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert - should not throw
		await sut.ReplicateKeysAsync("specific-key-id", CancellationToken.None);
	}

	[Fact]
	public async Task GetKeyAsync_DelegateToActiveProvider()
	{
		// Arrange
		var expectedKey = new KeyMetadata
		{
			KeyId = "key-1",
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow,
			Version = 1
		};

		_ = A.CallTo(() => _primaryProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(expectedKey));

		var sut = CreateSut();

		// Act
		var result = await sut.GetKeyAsync("key-1", CancellationToken.None);

		// Assert
		result.ShouldBe(expectedKey);
		_ = A.CallTo(() => _primaryProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetKeyAsync_DelegateToSecondaryProvider_WhenInFailoverMode()
	{
		// Arrange
		var expectedKey = new KeyMetadata
		{
			KeyId = "key-1",
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow,
			Version = 1
		};

		_ = A.CallTo(() => _secondaryProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(expectedKey));

		var sut = CreateSut();
		await sut.ForceFailoverAsync("Test failover", CancellationToken.None);

		// Act
		var result = await sut.GetKeyAsync("key-1", CancellationToken.None);

		// Assert
		result.ShouldBe(expectedKey);
		_ = A.CallTo(() => _secondaryProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ListKeysAsync_DelegateToActiveProvider()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		_ = await sut.ListKeysAsync(null, null, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _primaryProvider.ListKeysAsync(
				A<KeyStatus?>._,
				A<string?>._,
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RotateKeyAsync_DelegateToActiveProvider()
	{
		// Arrange
		var newKeyMeta = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 2,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};
		var expectedResult = KeyRotationResult.Succeeded(newKeyMeta);
		_ = A.CallTo(() => _primaryProvider.RotateKeyAsync(
				"key-1",
				EncryptionAlgorithm.Aes256Gcm,
				A<string?>._,
				A<DateTimeOffset?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		var sut = CreateSut();

		// Act
		var result = await sut.RotateKeyAsync("key-1", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.NewKey.KeyId.ShouldBe("key-1");
		result.NewKey.Version.ShouldBe(2);
	}

	[Fact]
	public async Task RotateKeyAsync_TriggerSynchronousReplication_WhenConfigured()
	{
		// Arrange
		_options.ReplicationMode = ReplicationMode.Synchronous;

		var newKeyMeta = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 2,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};
		var expectedResult = KeyRotationResult.Succeeded(newKeyMeta);
		_ = A.CallTo(() => _primaryProvider.RotateKeyAsync(
				"key-1",
				EncryptionAlgorithm.Aes256Gcm,
				A<string?>._,
				A<DateTimeOffset?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		var sut = CreateSut();

		// Act
		var result = await sut.RotateKeyAsync("key-1", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		// Replication would be triggered - verified by no exceptions
	}

	[Fact]
	public async Task DeleteKeyAsync_DelegateToActiveProvider()
	{
		// Arrange
		_ = A.CallTo(() => _primaryProvider.DeleteKeyAsync("key-1", 30, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		var sut = CreateSut();

		// Act
		var result = await sut.DeleteKeyAsync("key-1", 30, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => _primaryProvider.DeleteKeyAsync("key-1", 30, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SuspendKeyAsync_DelegateToActiveProvider()
	{
		// Arrange
		_ = A.CallTo(() => _primaryProvider.SuspendKeyAsync("key-1", "Compromised", A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		var sut = CreateSut();

		// Act
		var result = await sut.SuspendKeyAsync("key-1", "Compromised", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => _primaryProvider.SuspendKeyAsync("key-1", "Compromised", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetActiveKeyAsync_DelegateToActiveProvider()
	{
		// Arrange
		var expectedKey = new KeyMetadata
		{
			KeyId = "active-key",
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow,
			Version = 1
		};

		_ = A.CallTo(() => _primaryProvider.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(expectedKey));

		var sut = CreateSut();

		// Act
		var result = await sut.GetActiveKeyAsync(null, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedKey);
	}

	[Fact]
	public void DisposeSafely_WhenCalledMultipleTimes()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert - should not throw
		sut.Dispose();
		sut.Dispose();
	}

	[Fact]
	public async Task ThrowObjectDisposedException_AfterDisposal()
	{
		// Arrange
		var sut = CreateSut();
		sut.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			sut.GetPrimaryHealthAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ForceFailoverAsync_RespectCooldownPeriod()
	{
		// Arrange - set a short RTO target to act as cooldown
		_options.RtoTarget = TimeSpan.FromSeconds(60);
		var sut = CreateSut();

		// Perform first failover
		await sut.ForceFailoverAsync("First failover", CancellationToken.None);

		// Immediately failback
		await sut.FailbackToPrimaryAsync("Quick recovery", CancellationToken.None);

		// Act & Assert - should throw due to cooldown
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.ForceFailoverAsync("Second failover too soon", CancellationToken.None));

		ex.Message.ShouldContain("cooldown");
	}

	[Fact]
	public async Task GetKeyVersionAsync_DelegateToActiveProvider()
	{
		// Arrange
		var expectedKey = new KeyMetadata
		{
			KeyId = "key-1",
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow,
			Version = 2
		};

		_ = A.CallTo(() => _primaryProvider.GetKeyVersionAsync("key-1", 2, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(expectedKey));

		var sut = CreateSut();

		// Act
		var result = await sut.GetKeyVersionAsync("key-1", 2, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedKey);
		_ = A.CallTo(() => _primaryProvider.GetKeyVersionAsync("key-1", 2, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private static MultiRegionOptions CreateDefaultOptions() => new()
	{
		Primary = new RegionConfiguration
		{
			RegionId = "primary-region",
			Endpoint = new Uri("https://primary.example.com"),
			Priority = 0
		},
		Secondary = new RegionConfiguration
		{
			RegionId = "secondary-region",
			Endpoint = new Uri("https://secondary.example.com"),
			Priority = 1
		},
		EnableAutomaticFailover = false, // Disable for most tests to avoid timing issues
		HealthCheckInterval = TimeSpan.FromHours(1), // Long interval for tests
		OperationTimeout = TimeSpan.FromSeconds(5),
		RtoTarget = TimeSpan.FromMinutes(5),
		RpoTarget = TimeSpan.FromMinutes(15)
	};

	private MultiRegionKeyProvider CreateSut()
	{
		_sut = new MultiRegionKeyProvider(_primaryProvider, _secondaryProvider, _options, _logger);
		return _sut;
	}
}
