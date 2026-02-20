using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditLogEncryptionServiceShould
{
	private readonly IEncryptionProvider _encryptionProvider = A.Fake<IEncryptionProvider>();
	private readonly IKeyManagementProvider _keyManagementProvider = A.Fake<IKeyManagementProvider>();
	private readonly AuditLogEncryptionOptions _options = new()
	{
		EncryptFields = ["ActorId", "IpAddress"],
		KeyIdentifier = "test-audit-key"
	};

	private readonly NullLogger<AuditLogEncryptionService> _logger = NullLogger<AuditLogEncryptionService>.Instance;

	[Fact]
	public async Task Encrypt_specified_fields()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "test-audit-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		var entry = CreateAuditEvent();

		var encrypted = await sut.EncryptAsync(entry, CancellationToken.None).ConfigureAwait(false);

		encrypted.ShouldNotBeNull();
		encrypted.EventId.ShouldBe(entry.EventId);
		encrypted.KeyIdentifier.ShouldBe("test-audit-key");
		encrypted.EncryptedFields.ShouldContainKey("ActorId");
		encrypted.EncryptedFields.ShouldContainKey("IpAddress");
		encrypted.ClearFields.ShouldNotContainKey("ActorId");
	}

	[Fact]
	public async Task Place_unencrypted_fields_in_clear()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "test-audit-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		var entry = CreateAuditEvent();

		var encrypted = await sut.EncryptAsync(entry, CancellationToken.None).ConfigureAwait(false);

		encrypted.ClearFields.ShouldContainKey("Action");
		encrypted.ClearFields.ShouldContainKey("ResourceType");
	}

	[Fact]
	public async Task Throw_for_null_entry_on_encrypt()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.EncryptAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_entry_on_decrypt()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.DecryptAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Decrypt_encrypted_entry()
	{
		var plaintext = System.Text.Encoding.UTF8.GetBytes("test-actor");
		A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(plaintext);

		var sut = CreateService();
		var encryptedEntry = new EncryptedAuditEntry
		{
			EventId = "evt-1",
			EncryptedFields = new Dictionary<string, byte[]>
			{
				["ActorId"] = [1, 2, 3]
			},
			ClearFields = new Dictionary<string, string>
			{
				["Action"] = "Login",
				["EventType"] = "System",
				["Outcome"] = "Success",
				["Timestamp"] = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
			},
			KeyIdentifier = "test-audit-key",
			Algorithm = EncryptionAlgorithm.Aes256Gcm
		};

		var decrypted = await sut.DecryptAsync(encryptedEntry, CancellationToken.None).ConfigureAwait(false);

		decrypted.ShouldNotBeNull();
		decrypted.EventId.ShouldBe("evt-1");
		decrypted.ActorId.ShouldBe("test-actor");
		decrypted.Action.ShouldBe("Login");
	}

	[Fact]
	public async Task Propagate_encryption_exception()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Key unavailable"));

		var sut = CreateService();

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.EncryptAsync(CreateAuditEvent(), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_encryption_provider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AuditLogEncryptionService(
				null!,
				_keyManagementProvider,
				Microsoft.Extensions.Options.Options.Create(_options),
				_logger));
	}

	[Fact]
	public void Throw_for_null_key_management_provider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AuditLogEncryptionService(
				_encryptionProvider,
				null!,
				Microsoft.Extensions.Options.Options.Create(_options),
				_logger));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AuditLogEncryptionService(
				_encryptionProvider,
				_keyManagementProvider,
				null!,
				_logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AuditLogEncryptionService(
				_encryptionProvider,
				_keyManagementProvider,
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
	}

	private AuditLogEncryptionService CreateService() =>
		new(
			_encryptionProvider,
			_keyManagementProvider,
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

	private static AuditEvent CreateAuditEvent() =>
		new()
		{
			EventId = "evt-" + Guid.NewGuid().ToString("N")[..8],
			EventType = AuditEventType.Authentication,
			Action = "Login",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-123",
			ActorType = "User",
			ResourceId = "resource-1",
			ResourceType = "Session",
			IpAddress = "192.168.1.1"
		};
}
