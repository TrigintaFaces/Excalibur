using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

/// <summary>
/// Tests the multi-region key provider failover and failback workflow,
/// including operations routing to the active provider after failover.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MultiRegionKeyProviderWorkflowShould : IDisposable
{
	private readonly IKeyManagementProvider _primary = A.Fake<IKeyManagementProvider>();
	private readonly IKeyManagementProvider _secondary = A.Fake<IKeyManagementProvider>();
	private readonly MultiRegionKeyProvider _sut;

	public MultiRegionKeyProviderWorkflowShould()
	{
		var options = new MultiRegionOptions
		{
			Primary = new RegionConfiguration
			{
				RegionId = "us-east-1",
				Endpoint = new Uri("https://primary.example.com"),
			},
			Secondary = new RegionConfiguration
			{
				RegionId = "us-west-2",
				Endpoint = new Uri("https://secondary.example.com"),
			},
			HealthCheckInterval = TimeSpan.FromHours(1),
			EnableAutomaticFailover = false,
		};

		// Allow health check to succeed for both
		A.CallTo(() => _primary.ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));
		A.CallTo(() => _secondary.ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));

		_sut = new MultiRegionKeyProvider(
			_primary,
			_secondary,
			options,
			NullLogger<MultiRegionKeyProvider>.Instance);
	}

	[Fact]
	public async Task Route_get_key_to_secondary_after_failover()
	{
		// Arrange
		var expectedKey = new KeyMetadata
		{
			KeyId = "k1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
		};

		A.CallTo(() => _secondary.GetKeyAsync("k1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(expectedKey));

		// Act
		await _sut.ForceFailoverAsync("primary down", CancellationToken.None)
			.ConfigureAwait(false);

		var result = await _sut.GetKeyAsync("k1", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expectedKey);
		A.CallTo(() => _secondary.GetKeyAsync("k1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		// Primary should NOT have been called after failover
		A.CallTo(() => _primary.GetKeyAsync("k1", A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Route_rotate_key_to_secondary_after_failover()
	{
		// Arrange
		var expected = new KeyRotationResult { Success = true };
		A.CallTo(() => _secondary.RotateKeyAsync(
				"k1", EncryptionAlgorithm.Aes256Gcm, null, null, A<CancellationToken>._))
			.Returns(Task.FromResult(expected));

		// Act
		await _sut.ForceFailoverAsync("failover", CancellationToken.None)
			.ConfigureAwait(false);

		var result = await _sut.RotateKeyAsync(
				"k1", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Route_back_to_primary_after_failback()
	{
		// Arrange
		var expectedKey = new KeyMetadata
		{
			KeyId = "k2",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
		};

		A.CallTo(() => _primary.GetKeyAsync("k2", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(expectedKey));

		// Act - failover then failback
		await _sut.ForceFailoverAsync("failover", CancellationToken.None)
			.ConfigureAwait(false);
		await _sut.FailbackToPrimaryAsync("recovered", CancellationToken.None)
			.ConfigureAwait(false);

		var result = await _sut.GetKeyAsync("k2", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - should route back to primary
		result.ShouldBeSameAs(expectedKey);
		A.CallTo(() => _primary.GetKeyAsync("k2", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Route_delete_key_to_secondary_after_failover()
	{
		// Arrange
		A.CallTo(() => _secondary.DeleteKeyAsync("k1", 90, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		await _sut.ForceFailoverAsync("failover", CancellationToken.None)
			.ConfigureAwait(false);

		var result = await _sut.DeleteKeyAsync("k1", 90, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => _secondary.DeleteKeyAsync("k1", 90, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Route_suspend_key_to_secondary_after_failover()
	{
		// Arrange
		A.CallTo(() => _secondary.SuspendKeyAsync("k1", "security", A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		await _sut.ForceFailoverAsync("failover", CancellationToken.None)
			.ConfigureAwait(false);

		var result = await _sut.SuspendKeyAsync("k1", "security", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task Route_get_active_key_to_secondary_after_failover()
	{
		// Arrange
		var expected = new KeyMetadata
		{
			KeyId = "active-key",
			Version = 3,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
		};
		A.CallTo(() => _secondary.GetActiveKeyAsync("purpose", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(expected));

		// Act
		await _sut.ForceFailoverAsync("failover", CancellationToken.None)
			.ConfigureAwait(false);

		var result = await _sut.GetActiveKeyAsync("purpose", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Route_list_keys_to_secondary_after_failover()
	{
		// Arrange
		IReadOnlyList<KeyMetadata> expected = [
			new KeyMetadata
			{
				KeyId = "k1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			},
		];
		A.CallTo(() => _secondary.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(Task.FromResult(expected));

		// Act
		await _sut.ForceFailoverAsync("failover", CancellationToken.None)
			.ConfigureAwait(false);

		var result = await _sut.ListKeysAsync(KeyStatus.Active, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Reject_operations_after_dispose()
	{
		// Arrange
		_sut.Dispose();

		// Assert - all operations should throw ObjectDisposedException
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.GetKeyAsync("k1", CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.GetActiveKeyAsync(null, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.RotateKeyAsync("k1", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.ListKeysAsync(null, null, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.DeleteKeyAsync("k1", 0, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.SuspendKeyAsync("k1", "reason", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Report_replication_status_reflects_mode()
	{
		// Act - get status in normal mode
		var normalStatus = await _sut.GetReplicationStatusAsync(CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		normalStatus.ShouldNotBeNull();
		normalStatus.ReplicationMode.ShouldBe(ReplicationMode.Asynchronous);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}
}
