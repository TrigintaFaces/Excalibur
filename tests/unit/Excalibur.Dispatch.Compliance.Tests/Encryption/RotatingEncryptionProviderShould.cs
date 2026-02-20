using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RotatingEncryptionProviderShould : IDisposable
{
	private readonly IEncryptionProvider _inner = A.Fake<IEncryptionProvider>();
	private readonly IKeyManagementProvider _keyManagement = A.Fake<IKeyManagementProvider>();
	private readonly RotatingEncryptionProvider _sut;

	public RotatingEncryptionProviderShould()
	{
		_sut = new RotatingEncryptionProvider(
			_inner,
			_keyManagement,
			NullLogger<RotatingEncryptionProvider>.Instance);
	}

	[Fact]
	public async Task Delegate_encrypt_to_inner_provider()
	{
		// Arrange
		var plaintext = new byte[] { 1, 2, 3 };
		var context = new EncryptionContext();
		var expected = new EncryptedData
		{
			Ciphertext = [4, 5, 6],
			Iv = new byte[12],
			KeyId = "k1",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};

		A.CallTo(() => _inner.EncryptAsync(plaintext, context, A<CancellationToken>._))
			.Returns(Task.FromResult(expected));

		// Act
		var result = await _sut.EncryptAsync(plaintext, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Delegate_decrypt_to_inner_provider()
	{
		// Arrange
		var encrypted = new EncryptedData
		{
			Ciphertext = [1, 2, 3],
			Iv = new byte[12],
			KeyId = "k1",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};
		var context = new EncryptionContext();
		var expected = new byte[] { 10, 20, 30 };

		A.CallTo(() => _inner.DecryptAsync(encrypted, context, A<CancellationToken>._))
			.Returns(Task.FromResult(expected));
		A.CallTo(() => _keyManagement.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		// Act
		var result = await _sut.DecryptAsync(encrypted, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Delegate_fips_validation_to_inner()
	{
		// Arrange
		A.CallTo(() => _inner.ValidateFipsComplianceAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		var result = await _sut.ValidateFipsComplianceAsync(CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task Delegate_rotate_key_to_key_management()
	{
		// Arrange
		var expected = new KeyRotationResult
		{
			Success = true,
			NewKey = new KeyMetadata
			{
				KeyId = "k1",
				Version = 2,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			},
		};

		A.CallTo(() => _keyManagement.RotateKeyAsync("k1", EncryptionAlgorithm.Aes256Gcm, null, null, A<CancellationToken>._))
			.Returns(Task.FromResult(expected));

		// Act
		var result = await _sut.RotateKeyAsync("k1", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Auto_rotate_when_key_exceeds_max_age()
	{
		// Arrange
		var options = new RotatingEncryptionOptions
		{
			AutoRotateBeforeEncryption = true,
			MaxKeyAge = TimeSpan.FromDays(30),
		};
		var sut = new RotatingEncryptionProvider(
			_inner, _keyManagement,
			NullLogger<RotatingEncryptionProvider>.Instance,
			options);

		var oldKey = new KeyMetadata
		{
			KeyId = "k1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-60), // exceeds 30 day max
		};

		A.CallTo(() => _keyManagement.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(oldKey));
		A.CallTo(() => _keyManagement.RotateKeyAsync("k1", EncryptionAlgorithm.Aes256Gcm, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new KeyRotationResult
			{
				Success = true,
				NewKey = new KeyMetadata
				{
					KeyId = "k1",
					Version = 2,
					Status = KeyStatus.Active,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					CreatedAt = DateTimeOffset.UtcNow,
				},
			}));
		A.CallTo(() => _inner.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [1],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 2,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		// Act
		await sut.EncryptAsync([1, 2], new EncryptionContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - rotation was triggered
		A.CallTo(() => _keyManagement.RotateKeyAsync("k1", EncryptionAlgorithm.Aes256Gcm, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Not_auto_rotate_when_disabled()
	{
		// Arrange - default options have AutoRotateBeforeEncryption = false
		A.CallTo(() => _inner.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [1],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		// Act
		await _sut.EncryptAsync([1], new EncryptionContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - GetActiveKeyAsync should not be called (no rotation check)
		A.CallTo(() => _keyManagement.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReEncrypt_data_when_using_old_key()
	{
		// Arrange
		var oldEncrypted = new EncryptedData
		{
			Ciphertext = [1, 2, 3],
			Iv = new byte[12],
			KeyId = "k1",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};
		var context = new EncryptionContext();
		var plaintext = new byte[] { 10, 20, 30 };
		var newEncrypted = new EncryptedData
		{
			Ciphertext = [4, 5, 6],
			Iv = new byte[12],
			KeyId = "k1",
			KeyVersion = 2,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};

		A.CallTo(() => _keyManagement.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "k1",
				Version = 2,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));
		A.CallTo(() => _inner.DecryptAsync(oldEncrypted, context, A<CancellationToken>._))
			.Returns(Task.FromResult(plaintext));
		A.CallTo(() => _inner.EncryptAsync(A<byte[]>._, context, A<CancellationToken>._))
			.Returns(Task.FromResult(newEncrypted));

		// Act
		var result = await _sut.ReEncryptAsync(oldEncrypted, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(newEncrypted);
		A.CallTo(() => _inner.DecryptAsync(oldEncrypted, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _inner.EncryptAsync(A<byte[]>._, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Skip_reencryption_when_already_using_active_key()
	{
		// Arrange
		var encrypted = new EncryptedData
		{
			Ciphertext = [1, 2, 3],
			Iv = new byte[12],
			KeyId = "k1",
			KeyVersion = 2,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};
		var context = new EncryptionContext();

		A.CallTo(() => _keyManagement.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "k1",
				Version = 2,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		// Act
		var result = await _sut.ReEncryptAsync(encrypted, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - same object returned, no decrypt/encrypt calls
		result.ShouldBeSameAs(encrypted);
		A.CallTo(() => _inner.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Throw_on_encrypt_after_dispose()
	{
		// Arrange
		_sut.Dispose();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.EncryptAsync([1], new EncryptionContext(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_decrypt_after_dispose()
	{
		// Arrange
		_sut.Dispose();
		var encrypted = new EncryptedData
		{
			Ciphertext = [1],
			Iv = new byte[12],
			KeyId = "k1",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.DecryptAsync(encrypted, new EncryptionContext(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_fips_validation_after_dispose()
	{
		_sut.Dispose();
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.ValidateFipsComplianceAsync(CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_rotate_after_dispose()
	{
		_sut.Dispose();
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.RotateKeyAsync("k1", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_reencrypt_after_dispose()
	{
		_sut.Dispose();
		var encrypted = new EncryptedData
		{
			Ciphertext = [1],
			Iv = new byte[12],
			KeyId = "k1",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.ReEncryptAsync(encrypted, new EncryptionContext(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_inner()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RotatingEncryptionProvider(null!, _keyManagement, NullLogger<RotatingEncryptionProvider>.Instance));
	}

	[Fact]
	public void Throw_for_null_key_management()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RotatingEncryptionProvider(_inner, null!, NullLogger<RotatingEncryptionProvider>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RotatingEncryptionProvider(_inner, _keyManagement, null!));
	}

	[Fact]
	public async Task Throw_on_null_plaintext_for_encrypt()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.EncryptAsync(null!, new EncryptionContext(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_encrypted_data_for_decrypt()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.DecryptAsync(null!, new EncryptionContext(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_encrypted_data_for_reencrypt()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReEncryptAsync(null!, new EncryptionContext(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public void Use_default_options_when_null()
	{
		// Act - should not throw, uses defaults
		using var sut = new RotatingEncryptionProvider(
			_inner, _keyManagement,
			NullLogger<RotatingEncryptionProvider>.Instance,
			null);

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void Have_default_max_key_age_of_90_days()
	{
		var options = new RotatingEncryptionOptions();
		options.MaxKeyAge.ShouldBe(TimeSpan.FromDays(90));
	}

	[Fact]
	public void Have_default_auto_rotate_disabled()
	{
		var options = new RotatingEncryptionOptions();
		options.AutoRotateBeforeEncryption.ShouldBeFalse();
	}

	[Fact]
	public void Have_default_reencrypt_on_read_enabled()
	{
		var options = new RotatingEncryptionOptions();
		options.ReEncryptOnRead.ShouldBeTrue();
	}

	[Fact]
	public void Dispose_inner_when_disposable()
	{
		// Arrange
		var disposableInner = A.Fake<IEncryptionProvider>(o => o.Implements<IDisposable>());
		var sut = new RotatingEncryptionProvider(
			disposableInner, _keyManagement,
			NullLogger<RotatingEncryptionProvider>.Instance);

		// Act
		sut.Dispose();

		// Assert
		A.CallTo(() => ((IDisposable)disposableInner).Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Allow_double_dispose()
	{
		_sut.Dispose();
		_sut.Dispose(); // Should not throw
	}

	public void Dispose()
	{
		_sut.Dispose();
	}
}
