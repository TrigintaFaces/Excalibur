using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.KeyRotation;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyRotationServiceShould : IDisposable
{
	private readonly IKeyManagementProvider _keyProvider = A.Fake<IKeyManagementProvider>();
	private readonly KeyRotationOptions _options = new()
	{
		Enabled = true,
		CheckInterval = TimeSpan.FromMinutes(1),
		MaxConcurrentRotations = 2,
		ContinueOnError = true,
		RetryDelay = TimeSpan.FromSeconds(1),
		RotationTimeout = TimeSpan.FromMinutes(1)
	};

	private readonly NullLogger<KeyRotationService> _logger = NullLogger<KeyRotationService>.Instance;

	[Fact]
	public async Task Check_and_rotate_keys_that_are_due()
	{
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 100);
		A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(new List<KeyMetadata> { key });
		A.CallTo(() => _keyProvider.RotateKeyAsync("key-1", A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(KeyRotationResult.Succeeded(CreateKeyMetadata("key-1", version: 2, createdDaysAgo: 0)));

		var sut = CreateService();

		var result = await sut.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

		result.KeysChecked.ShouldBe(1);
		result.KeysDueForRotation.ShouldBe(1);
		result.KeysRotated.ShouldBe(1);
		result.KeysFailed.ShouldBe(0);
	}

	[Fact]
	public async Task Skip_keys_not_due_for_rotation()
	{
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 10);
		A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(new List<KeyMetadata> { key });

		var sut = CreateService();

		var result = await sut.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

		result.KeysChecked.ShouldBe(1);
		result.KeysDueForRotation.ShouldBe(0);
		result.KeysRotated.ShouldBe(0);
		A.CallTo(() => _keyProvider.RotateKeyAsync(A<string>._, A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Handle_rotation_failure_with_continue_on_error()
	{
		var key1 = CreateKeyMetadata("key-1", createdDaysAgo: 100);
		var key2 = CreateKeyMetadata("key-2", createdDaysAgo: 100);
		A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(new List<KeyMetadata> { key1, key2 });
		A.CallTo(() => _keyProvider.RotateKeyAsync("key-1", A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(KeyRotationResult.Failed("Provider error"));
		A.CallTo(() => _keyProvider.RotateKeyAsync("key-2", A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(KeyRotationResult.Succeeded(CreateKeyMetadata("key-2", version: 2, createdDaysAgo: 0)));

		var sut = CreateService();

		var result = await sut.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

		result.KeysDueForRotation.ShouldBe(2);
		result.KeysRotated.ShouldBe(1);
		result.KeysFailed.ShouldBe(1);
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Stop_on_first_failure_when_continue_on_error_disabled()
	{
		_options.ContinueOnError = false;
		var key1 = CreateKeyMetadata("key-1", createdDaysAgo: 100);
		var key2 = CreateKeyMetadata("key-2", createdDaysAgo: 100);
		A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(new List<KeyMetadata> { key1, key2 });
		A.CallTo(() => _keyProvider.RotateKeyAsync("key-1", A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(KeyRotationResult.Failed("Provider error"));

		var sut = CreateService();

		var result = await sut.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

		result.KeysFailed.ShouldBe(1);
		A.CallTo(() => _keyProvider.RotateKeyAsync("key-2", A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Is_rotation_due_returns_true_for_old_key()
	{
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 100);
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(key);

		var sut = CreateService();

		var isDue = await sut.IsRotationDueAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		isDue.ShouldBeTrue();
	}

	[Fact]
	public async Task Is_rotation_due_returns_false_for_recent_key()
	{
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 10);
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(key);

		var sut = CreateService();

		var isDue = await sut.IsRotationDueAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		isDue.ShouldBeFalse();
	}

	[Fact]
	public async Task Is_rotation_due_returns_false_for_unknown_key()
	{
		A.CallTo(() => _keyProvider.GetKeyAsync("unknown", A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		var sut = CreateService();

		var isDue = await sut.IsRotationDueAsync("unknown", CancellationToken.None).ConfigureAwait(false);

		isDue.ShouldBeFalse();
	}

	[Fact]
	public async Task Is_rotation_due_throws_for_null_key_id()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsRotationDueAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Is_rotation_due_throws_for_empty_key_id()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.IsRotationDueAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Force_rotate_succeeds_for_existing_key()
	{
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 10);
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(key);
		A.CallTo(() => _keyProvider.RotateKeyAsync("key-1", A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(KeyRotationResult.Succeeded(CreateKeyMetadata("key-1", version: 2, createdDaysAgo: 0)));

		var sut = CreateService();

		var result = await sut.ForceRotateAsync("key-1", "security incident", CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task Force_rotate_returns_failure_for_unknown_key()
	{
		A.CallTo(() => _keyProvider.GetKeyAsync("unknown", A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		var sut = CreateService();

		var result = await sut.ForceRotateAsync("unknown", "test", CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("not found");
	}

	[Fact]
	public async Task Force_rotate_throws_for_null_key_id()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.ForceRotateAsync(null!, "reason", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Force_rotate_throws_for_null_reason()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.ForceRotateAsync("key-1", null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Get_next_rotation_time_returns_value_for_existing_key()
	{
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 10);
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(key);

		var sut = CreateService();

		var nextRotation = await sut.GetNextRotationTimeAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		nextRotation.ShouldNotBeNull();
	}

	[Fact]
	public async Task Get_next_rotation_time_returns_null_for_unknown_key()
	{
		A.CallTo(() => _keyProvider.GetKeyAsync("unknown", A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		var sut = CreateService();

		var nextRotation = await sut.GetNextRotationTimeAsync("unknown", CancellationToken.None).ConfigureAwait(false);

		nextRotation.ShouldBeNull();
	}

	[Fact]
	public async Task Get_next_rotation_time_returns_null_when_auto_rotate_disabled()
	{
		_options.DefaultPolicy = KeyRotationPolicy.Default with { AutoRotateEnabled = false };
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 10);
		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(key);

		var sut = CreateService();

		var nextRotation = await sut.GetNextRotationTimeAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		nextRotation.ShouldBeNull();
	}

	[Fact]
	public void Dispose_releases_resources()
	{
		var sut = CreateService();

		Should.NotThrow(() => sut.Dispose());
	}

	[Fact]
	public async Task Handle_exception_during_rotation_with_continue_on_error()
	{
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 100);
		A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(new List<KeyMetadata> { key });
		A.CallTo(() => _keyProvider.RotateKeyAsync("key-1", A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Provider crashed"));

		var sut = CreateService();

		var result = await sut.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

		result.KeysFailed.ShouldBe(1);
		result.Errors.ShouldNotBeEmpty();
		result.Errors[0].Message.ShouldBe("Provider crashed");
	}

	[Fact]
	public async Task Use_purpose_specific_policy()
	{
		_options.AddHighSecurityPolicy("payment-keys");
		var key = CreateKeyMetadata("key-1", createdDaysAgo: 35, purpose: "payment-keys");
		A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(new List<KeyMetadata> { key });
		A.CallTo(() => _keyProvider.RotateKeyAsync("key-1", A<EncryptionAlgorithm>._, "payment-keys", A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(KeyRotationResult.Succeeded(CreateKeyMetadata("key-1", version: 2, createdDaysAgo: 0, purpose: "payment-keys")));

		var sut = CreateService();

		var result = await sut.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

		result.KeysDueForRotation.ShouldBe(1);
		result.KeysRotated.ShouldBe(1);
	}

	[Fact]
	public async Task Return_completed_at_timestamps()
	{
		A.CallTo(() => _keyProvider.ListKeysAsync(KeyStatus.Active, null, A<CancellationToken>._))
			.Returns(new List<KeyMetadata>());

		var sut = CreateService();
		var before = DateTimeOffset.UtcNow;

		var result = await sut.CheckAndRotateAsync(CancellationToken.None).ConfigureAwait(false);

		result.StartedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.CompletedAt.ShouldBeGreaterThanOrEqualTo(result.StartedAt);
	}

	public void Dispose()
	{
		// No explicit cleanup needed - service is created per test
	}

	private static KeyMetadata CreateKeyMetadata(
		string keyId,
		int version = 1,
		int createdDaysAgo = 0,
		string? purpose = null) =>
		new()
		{
			KeyId = keyId,
			Version = version,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-createdDaysAgo),
			Purpose = purpose
		};

	private KeyRotationService CreateService() =>
		new(_keyProvider, Microsoft.Extensions.Options.Options.Create(_options), _logger);
}
