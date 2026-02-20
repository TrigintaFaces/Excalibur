namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class NullKeyCacheShould
{
	private readonly NullKeyCache _sut = NullKeyCache.Instance;

	[Fact]
	public void Be_singleton()
	{
		NullKeyCache.Instance.ShouldBeSameAs(NullKeyCache.Instance);
	}

	[Fact]
	public void Have_zero_count()
	{
		_sut.Count.ShouldBe(0);
	}

	[Fact]
	public void Return_null_from_try_get()
	{
		_sut.TryGet("any-key").ShouldBeNull();
	}

	[Fact]
	public async Task Always_call_factory_for_get_or_add()
	{
		// Arrange
		var callCount = 0;
		var expected = CreateKeyMetadata("key-1");

		// Act
		var result1 = await _sut.GetOrAddAsync(
			"key-1",
			(_, _) =>
			{
				callCount++;
				return Task.FromResult<KeyMetadata?>(expected);
			},
			CancellationToken.None).ConfigureAwait(false);

		var result2 = await _sut.GetOrAddAsync(
			"key-1",
			(_, _) =>
			{
				callCount++;
				return Task.FromResult<KeyMetadata?>(expected);
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert - factory should be called every time (no caching)
		callCount.ShouldBe(2);
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
	}

	[Fact]
	public async Task Always_call_factory_for_get_or_add_with_ttl()
	{
		// Arrange
		var callCount = 0;
		var expected = CreateKeyMetadata("key-1");

		// Act
		await _sut.GetOrAddAsync(
			"key-1",
			TimeSpan.FromMinutes(5),
			(_, _) =>
			{
				callCount++;
				return Task.FromResult<KeyMetadata?>(expected);
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		callCount.ShouldBe(1);
	}

	[Fact]
	public void Not_throw_on_set()
	{
		// Act & Assert - no-op, should not throw
		_sut.Set(CreateKeyMetadata("key-1"));
	}

	[Fact]
	public void Not_throw_on_set_with_ttl()
	{
		// Act & Assert - no-op, should not throw
		_sut.Set(CreateKeyMetadata("key-1"), TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Not_throw_on_remove()
	{
		// Act & Assert - no-op, should not throw
		_sut.Remove("key-1");
	}

	[Fact]
	public void Not_throw_on_invalidate()
	{
		// Act & Assert - no-op, should not throw
		_sut.Invalidate("key-1");
	}

	[Fact]
	public void Not_throw_on_clear()
	{
		// Act & Assert - no-op, should not throw
		_sut.Clear();
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
			() => _sut.GetOrAddAsync("key-1", null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_key_id_for_get_or_add_with_ttl()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.GetOrAddAsync(
				null!,
				TimeSpan.FromMinutes(5),
				(_, _) => Task.FromResult<KeyMetadata?>(null),
				CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_factory_for_get_or_add_with_ttl()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.GetOrAddAsync(
				"key-1",
				TimeSpan.FromMinutes(5),
				null!,
				CancellationToken.None)).ConfigureAwait(false);
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
