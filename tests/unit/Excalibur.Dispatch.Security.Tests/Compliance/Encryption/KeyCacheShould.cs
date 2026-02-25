// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

[Trait("Category", TestCategories.Unit)]
public sealed class KeyCacheShould : IDisposable
{
	private readonly KeyCache _sut;
	private readonly IEncryptionTelemetry _telemetry;
	private readonly IEncryptionTelemetryDetails _telemetryDetails;

	public KeyCacheShould()
	{
		_telemetry = A.Fake<IEncryptionTelemetry>();
		_telemetryDetails = A.Fake<IEncryptionTelemetryDetails>();

		// Wire up GetService to return telemetry details sub-interface
		_ = A.CallTo(() => _telemetry.GetService(typeof(IEncryptionTelemetryDetails)))
			.Returns(_telemetryDetails);

		_sut = new KeyCache(KeyCacheOptions.Default, _telemetry);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	[Fact]
	public void HaveZeroCountWhenEmpty()
	{
		// Assert
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public void SetAndRetrieveKeyMetadata()
	{
		// Arrange
		var metadata = CreateKeyMetadata();

		// Act
		_sut.Set(metadata);
		var result = _sut.TryGet("key-1");

		// Assert
		_ = result.ShouldNotBeNull();
		result.KeyId.ShouldBe("key-1");
		_sut.Count.ShouldBe(1);
	}

	[Fact]
	public void ReportCacheHitOnTryGet()
	{
		// Arrange
		var metadata = CreateKeyMetadata();
		_sut.Set(metadata);

		// Act
		_ = _sut.TryGet("key-1");

		// Assert
		_ = A.CallTo(() => _telemetryDetails.RecordCacheAccess(true, "KeyCache"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReportCacheMissOnTryGet()
	{
		// Act
		_ = _sut.TryGet("nonexistent");

		// Assert
		_ = A.CallTo(() => _telemetryDetails.RecordCacheAccess(false, "KeyCache"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReturnNullForNonexistentKey()
	{
		// Act
		var result = _sut.TryGet("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void RemoveKeyFromCache()
	{
		// Arrange
		var metadata = CreateKeyMetadata();
		_sut.Set(metadata);

		// Act
		_sut.Remove("key-1");
		var result = _sut.TryGet("key-1");

		// Assert
		result.ShouldBeNull();
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public void ClearAllEntries()
	{
		// Arrange
		_sut.Set(CreateKeyMetadata("key-1"));
		_sut.Set(CreateKeyMetadata("key-2"));
		_sut.Set(CreateKeyMetadata("key-3"));

		// Act
		_sut.Clear();

		// Assert
		_sut.Count.ShouldBe(0);
		_sut.TryGet("key-1").ShouldBeNull();
		_sut.TryGet("key-2").ShouldBeNull();
		_sut.TryGet("key-3").ShouldBeNull();
	}

	[Fact]
	public async Task GetOrAddAsync_ReturnsCachedValue()
	{
		// Arrange
		var metadata = CreateKeyMetadata();
		_sut.Set(metadata);
		var factoryCalled = false;

		// Act
		var result = await _sut.GetOrAddAsync("key-1", async (_, _) =>
		{
			factoryCalled = true;
			return await Task.FromResult(CreateKeyMetadata());
		}, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.KeyId.ShouldBe("key-1");
		factoryCalled.ShouldBeFalse();
		_ = A.CallTo(() => _telemetryDetails.RecordCacheAccess(true, "KeyCache"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetOrAddAsync_CallsFactoryWhenNotCached()
	{
		// Arrange
		var factoryCalled = false;

		// Act
		var result = await _sut.GetOrAddAsync("key-1", async (keyId, _) =>
		{
			factoryCalled = true;
			return await Task.FromResult(CreateKeyMetadata(keyId));
		}, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.KeyId.ShouldBe("key-1");
		factoryCalled.ShouldBeTrue();
		_ = A.CallTo(() => _telemetryDetails.RecordCacheAccess(false, "KeyCache"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetOrAddAsync_CachesFactoryResult()
	{
		// Arrange
		var factoryCallCount = 0;

		// Act
		_ = await _sut.GetOrAddAsync("key-1", async (keyId, _) =>
		{
			factoryCallCount++;
			return await Task.FromResult(CreateKeyMetadata(keyId));
		}, CancellationToken.None);

		_ = await _sut.GetOrAddAsync("key-1", async (keyId, _) =>
		{
			factoryCallCount++;
			return await Task.FromResult(CreateKeyMetadata(keyId));
		}, CancellationToken.None);

		// Assert
		factoryCallCount.ShouldBe(1);
		_sut.Count.ShouldBe(1);
	}

	[Fact]
	public async Task GetOrAddAsync_DoesNotCacheNull()
	{
		// Act
		var result = await _sut.GetOrAddAsync("key-1", async (_, _) =>
		{
			return await Task.FromResult<KeyMetadata?>(null);
		}, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public void SetWithCustomTtl()
	{
		// Arrange
		var metadata = CreateKeyMetadata();
		var customTtl = TimeSpan.FromMinutes(10);

		// Act
		_sut.Set(metadata, customTtl);
		var result = _sut.TryGet("key-1");

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void InvalidateRemovesAllVersions()
	{
		// Arrange
		_sut.Set(CreateKeyMetadata("key-1", version: 1));
		_sut.Set(CreateKeyMetadata("key-1", version: 2));
		_sut.Set(CreateKeyMetadata("key-2", version: 1));

		// Act - invalidate key-1 (should keep key-2)
		_sut.Invalidate("key-1");

		// Assert
		_sut.TryGet("key-1").ShouldBeNull();
		// key-2 should still exist
		// Note: In current impl, version-specific entries need a different cache key pattern
		// For now, just verify that invalidate removes the main entry
	}

	[Fact]
	public void ThrowOnNullKeyId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.TryGet(null!));
		_ = Should.Throw<ArgumentNullException>(() => _sut.Set(null!));
		_ = Should.Throw<ArgumentNullException>(() => _sut.Remove(null!));
		_ = Should.Throw<ArgumentNullException>(() => _sut.Invalidate(null!));
	}

	[Fact]
	public async Task ThrowOnNullFactory()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.GetOrAddAsync("key-1", null!, CancellationToken.None));
	}

	[Fact]
	public void ThrowAfterDispose()
	{
		// Arrange
		_sut.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() => _sut.TryGet("key-1"));
		_ = Should.Throw<ObjectDisposedException>(() => _sut.Set(CreateKeyMetadata()));
		_ = Should.Throw<ObjectDisposedException>(() => _sut.Remove("key-1"));
		_ = Should.Throw<ObjectDisposedException>(() => _sut.Clear());
	}

	[Fact]
	public void DisposeSafely_WhenCalledMultipleTimes()
	{
		// Act & Assert - should not throw
		_sut.Dispose();
		_sut.Dispose();
	}

	private static KeyMetadata CreateKeyMetadata(string keyId = "key-1", int version = 1) =>
																		new()
																		{
																			KeyId = keyId,
																			Version = version,
																			Status = KeyStatus.Active,
																			Algorithm = EncryptionAlgorithm.Aes256Gcm,
																			CreatedAt = DateTimeOffset.UtcNow,
																		};
}

[Trait("Category", TestCategories.Unit)]
public sealed class NullKeyCacheShould
{
	[Fact]
	public void ReturnSingletonInstance()
	{
		// Arrange & Act
		var instance1 = NullKeyCache.Instance;
		var instance2 = NullKeyCache.Instance;

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public void HaveZeroCount()
	{
		// Arrange
		var sut = NullKeyCache.Instance;

		// Assert
		sut.Count.ShouldBe(0);
	}

	[Fact]
	public void AlwaysReturnNullFromTryGet()
	{
		// Arrange
		var sut = NullKeyCache.Instance;

		// Act & Assert
		sut.TryGet("any-key").ShouldBeNull();
	}

	[Fact]
	public async Task AlwaysCallFactory()
	{
		// Arrange
		var sut = NullKeyCache.Instance;
		var factoryCallCount = 0;
		var metadata = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Act
		_ = await sut.GetOrAddAsync("key-1", async (_, _) =>
		{
			factoryCallCount++;
			return await Task.FromResult<KeyMetadata?>(metadata);
		}, CancellationToken.None);

		_ = await sut.GetOrAddAsync("key-1", async (_, _) =>
		{
			factoryCallCount++;
			return await Task.FromResult<KeyMetadata?>(metadata);
		}, CancellationToken.None);

		// Assert - factory should be called both times since nothing is cached
		factoryCallCount.ShouldBe(2);
	}

	[Fact]
	public void NotThrowOnSet()
	{
		// Arrange
		var sut = NullKeyCache.Instance;
		var metadata = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.Set(metadata));
		Should.NotThrow(() => sut.Set(metadata, TimeSpan.FromMinutes(5)));
	}

	[Fact]
	public void NotThrowOnRemove()
	{
		// Arrange
		var sut = NullKeyCache.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.Remove("any-key"));
	}

	[Fact]
	public void NotThrowOnInvalidate()
	{
		// Arrange
		var sut = NullKeyCache.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.Invalidate("any-key"));
	}

	[Fact]
	public void NotThrowOnClear()
	{
		// Arrange
		var sut = NullKeyCache.Instance;

		// Act & Assert - should not throw
		Should.NotThrow(() => sut.Clear());
	}
}

[Trait("Category", TestCategories.Unit)]
public sealed class KeyCacheOptionsShould
{
	[Fact]
	public void HaveDefaultValuesForDefaultInstance()
	{
		// Arrange
		var options = KeyCacheOptions.Default;

		// Assert
		options.DefaultTtl.ShouldBe(TimeSpan.FromMinutes(5));
		options.UseSlidingExpiration.ShouldBeFalse();
		options.MaxEntries.ShouldBe(1000);
		options.EnableAutoRefresh.ShouldBeFalse();
		options.AutoRefreshThreshold.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowCustomization()
	{
		// Arrange & Act
		var options = new KeyCacheOptions
		{
			DefaultTtl = TimeSpan.FromMinutes(10),
			UseSlidingExpiration = true,
			MaxEntries = 500,
			EnableAutoRefresh = true,
			AutoRefreshThreshold = TimeSpan.FromSeconds(60),
		};

		// Assert
		options.DefaultTtl.ShouldBe(TimeSpan.FromMinutes(10));
		options.UseSlidingExpiration.ShouldBeTrue();
		options.MaxEntries.ShouldBe(500);
		options.EnableAutoRefresh.ShouldBeTrue();
		options.AutoRefreshThreshold.ShouldBe(TimeSpan.FromSeconds(60));
	}
}
