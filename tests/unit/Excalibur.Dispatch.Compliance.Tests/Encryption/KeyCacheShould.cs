namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyCacheShould : IDisposable
{
	private readonly KeyCache _sut;

	public KeyCacheShould()
	{
		_sut = new KeyCache(KeyCacheOptions.Default);
	}

	[Fact]
	public void Have_zero_count_when_empty()
	{
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public void Set_and_retrieve_key_metadata()
	{
		// Arrange
		var metadata = CreateKeyMetadata("key-1");

		// Act
		_sut.Set(metadata);
		var result = _sut.TryGet("key-1");

		// Assert
		result.ShouldNotBeNull();
		result.KeyId.ShouldBe("key-1");
	}

	[Fact]
	public void Return_null_for_missing_key()
	{
		// Act
		var result = _sut.TryGet("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Track_count_correctly()
	{
		// Arrange & Act
		_sut.Set(CreateKeyMetadata("key-1"));
		_sut.Set(CreateKeyMetadata("key-2"));

		// Assert
		_sut.Count.ShouldBe(2);
	}

	[Fact]
	public void Remove_entry_by_key_id()
	{
		// Arrange
		_sut.Set(CreateKeyMetadata("key-1"));

		// Act
		_sut.Remove("key-1");

		// Assert
		_sut.TryGet("key-1").ShouldBeNull();
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public void Invalidate_key_and_all_versions()
	{
		// Arrange
		_sut.Set(CreateKeyMetadata("key-1"));

		// Act
		_sut.Invalidate("key-1");

		// Assert
		_sut.TryGet("key-1").ShouldBeNull();
	}

	[Fact]
	public void Clear_all_entries()
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
	public async Task GetOrAdd_returns_cached_value_on_hit()
	{
		// Arrange
		var metadata = CreateKeyMetadata("key-1");
		_sut.Set(metadata);

		var factoryCalled = false;

		// Act
		var result = await _sut.GetOrAddAsync(
			"key-1",
			(_, _) =>
			{
				factoryCalled = true;
				return Task.FromResult<KeyMetadata?>(CreateKeyMetadata("key-1-factory"));
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.KeyId.ShouldBe("key-1");
		factoryCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task GetOrAdd_invokes_factory_on_miss()
	{
		// Arrange
		var factoryCalled = false;

		// Act
		var result = await _sut.GetOrAddAsync(
			"key-1",
			(_, _) =>
			{
				factoryCalled = true;
				return Task.FromResult<KeyMetadata?>(CreateKeyMetadata("key-1"));
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		factoryCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task GetOrAdd_caches_factory_result()
	{
		// Arrange
		var callCount = 0;

		// Act
		await _sut.GetOrAddAsync(
			"key-1",
			(_, _) =>
			{
				callCount++;
				return Task.FromResult<KeyMetadata?>(CreateKeyMetadata("key-1"));
			},
			CancellationToken.None).ConfigureAwait(false);

		await _sut.GetOrAddAsync(
			"key-1",
			(_, _) =>
			{
				callCount++;
				return Task.FromResult<KeyMetadata?>(CreateKeyMetadata("key-1"));
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		callCount.ShouldBe(1);
	}

	[Fact]
	public async Task GetOrAdd_with_custom_ttl()
	{
		// Act
		var result = await _sut.GetOrAddAsync(
			"key-1",
			TimeSpan.FromMinutes(10),
			(_, _) => Task.FromResult<KeyMetadata?>(CreateKeyMetadata("key-1")),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		_sut.TryGet("key-1").ShouldNotBeNull();
	}

	[Fact]
	public async Task GetOrAdd_does_not_cache_null_result()
	{
		// Arrange
		var callCount = 0;

		// Act
		var result1 = await _sut.GetOrAddAsync(
			"key-1",
			(_, _) =>
			{
				callCount++;
				return Task.FromResult<KeyMetadata?>(null);
			},
			CancellationToken.None).ConfigureAwait(false);

		var result2 = await _sut.GetOrAddAsync(
			"key-1",
			(_, _) =>
			{
				callCount++;
				return Task.FromResult<KeyMetadata?>(null);
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result1.ShouldBeNull();
		result2.ShouldBeNull();
		callCount.ShouldBe(2);
	}

	[Fact]
	public void Set_with_custom_ttl()
	{
		// Act
		_sut.Set(CreateKeyMetadata("key-1"), TimeSpan.FromMinutes(30));

		// Assert
		_sut.TryGet("key-1").ShouldNotBeNull();
	}

	[Fact]
	public void Throw_on_null_key_metadata_for_set()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Set(null!));
	}

	[Fact]
	public void Throw_on_null_key_metadata_for_set_with_ttl()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Set(null!, TimeSpan.FromMinutes(5)));
	}

	[Fact]
	public void Throw_on_null_key_id_for_try_get()
	{
		Should.Throw<ArgumentNullException>(() => _sut.TryGet(null!));
	}

	[Fact]
	public void Throw_on_null_key_id_for_remove()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Remove(null!));
	}

	[Fact]
	public void Throw_on_null_key_id_for_invalidate()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Invalidate(null!));
	}

	[Fact]
	public async Task Throw_on_null_key_id_for_get_or_add()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.GetOrAddAsync(
				null!,
				(_, _) => Task.FromResult<KeyMetadata?>(null),
				CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_factory_for_get_or_add()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.GetOrAddAsync(
				"key-1",
				null!,
				CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_on_disposed_try_get()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.TryGet("key-1"));
	}

	[Fact]
	public void Throw_on_disposed_set()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.Set(CreateKeyMetadata("key-1")));
	}

	[Fact]
	public void Throw_on_disposed_remove()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.Remove("key-1"));
	}

	[Fact]
	public void Throw_on_disposed_invalidate()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.Invalidate("key-1"));
	}

	[Fact]
	public void Throw_on_disposed_clear()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.Clear());
	}

	[Fact]
	public void Allow_double_dispose()
	{
		_sut.Dispose();
		_sut.Dispose(); // Should not throw
	}

	[Fact]
	public void Create_with_default_constructor()
	{
		// Act
		using var cache = new KeyCache();

		// Assert
		cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Throw_on_null_options()
	{
		Should.Throw<ArgumentNullException>(() => new KeyCache((KeyCacheOptions)null!));
	}

	[Fact]
	public void Create_with_sliding_expiration()
	{
		// Arrange
		var options = new KeyCacheOptions { UseSlidingExpiration = true };
		using var cache = new KeyCache(options);

		// Act
		cache.Set(CreateKeyMetadata("key-1"));

		// Assert
		cache.TryGet("key-1").ShouldNotBeNull();
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	private static KeyMetadata CreateKeyMetadata(string keyId) => new()
	{
		KeyId = keyId,
		Version = 1,
		Status = KeyStatus.Active,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		CreatedAt = DateTimeOffset.UtcNow,
	};
}
