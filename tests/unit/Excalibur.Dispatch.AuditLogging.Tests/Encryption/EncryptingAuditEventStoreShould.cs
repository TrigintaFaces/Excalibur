using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.AuditLogging.Encryption;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Tests.Encryption;

public class EncryptingAuditEventStoreShould
{
    private readonly IAuditStore _innerStore = A.Fake<IAuditStore>();
    private readonly IEncryptionProvider _encryption = A.Fake<IEncryptionProvider>();

    private EncryptingAuditEventStore CreateSut(AuditEncryptionOptions? options = null) =>
        new(
            _innerStore,
            _encryption,
            Microsoft.Extensions.Options.Options.Create(options ?? new AuditEncryptionOptions()));

    private static AuditEvent CreateEvent(
        string actorId = "user-1",
        string? ipAddress = "192.168.1.1",
        string? reason = null,
        string? userAgent = null) =>
        new()
        {
            EventId = "evt-1",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = actorId,
            IpAddress = ipAddress,
            Reason = reason,
            UserAgent = userAgent
        };

    private void SetupEncryption()
    {
        A.CallTo(() => _encryption.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
            .ReturnsLazily((byte[] plaintext, EncryptionContext _, CancellationToken _) =>
            {
                var encrypted = new EncryptedData
                {
                    Ciphertext = plaintext,
                    KeyId = "key-1",
                    KeyVersion = 1,
                    Algorithm = EncryptionAlgorithm.Aes256Gcm,
                    Iv = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]
                };
                return Task.FromResult(encrypted);
            });

        A.CallTo(() => _encryption.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
            .ReturnsLazily((EncryptedData data, EncryptionContext _, CancellationToken _) =>
                Task.FromResult(data.Ciphertext));
    }

    [Fact]
    public async Task Encrypt_actor_id_and_ip_address_by_default()
    {
        SetupEncryption();
        var sut = CreateSut();
        var auditEvent = CreateEvent(actorId: "sensitive-user", ipAddress: "10.0.0.1");

        AuditEvent? capturedEvent = null;
        A.CallTo(() => _innerStore.StoreAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent evt, CancellationToken _) => capturedEvent = evt)
            .Returns(new AuditEventId
            {
                EventId = "evt-1",
                EventHash = "hash",
                SequenceNumber = 1,
                RecordedAt = DateTimeOffset.UtcNow
            });

        await sut.StoreAsync(auditEvent, CancellationToken.None);

        capturedEvent.ShouldNotBeNull();
        // Encrypted fields should be different from original
        capturedEvent.ActorId.ShouldNotBe("sensitive-user");
        capturedEvent.IpAddress.ShouldNotBe("10.0.0.1");
    }

    [Fact]
    public async Task Not_encrypt_reason_and_user_agent_by_default()
    {
        SetupEncryption();
        var sut = CreateSut();
        var auditEvent = CreateEvent(reason: "test-reason", userAgent: "TestAgent/1.0");

        AuditEvent? capturedEvent = null;
        A.CallTo(() => _innerStore.StoreAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent evt, CancellationToken _) => capturedEvent = evt)
            .Returns(new AuditEventId
            {
                EventId = "evt-1",
                EventHash = "hash",
                SequenceNumber = 1,
                RecordedAt = DateTimeOffset.UtcNow
            });

        await sut.StoreAsync(auditEvent, CancellationToken.None);

        capturedEvent.ShouldNotBeNull();
        capturedEvent.Reason.ShouldBe("test-reason");
        capturedEvent.UserAgent.ShouldBe("TestAgent/1.0");
    }

    [Fact]
    public async Task Encrypt_all_fields_when_configured()
    {
        SetupEncryption();
        var options = new AuditEncryptionOptions
        {
            EncryptActorId = true,
            EncryptIpAddress = true,
            EncryptReason = true,
            EncryptUserAgent = true
        };
        var sut = CreateSut(options);
        var auditEvent = CreateEvent(
            actorId: "user-1",
            ipAddress: "10.0.0.1",
            reason: "test-reason",
            userAgent: "TestAgent/1.0");

        AuditEvent? capturedEvent = null;
        A.CallTo(() => _innerStore.StoreAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent evt, CancellationToken _) => capturedEvent = evt)
            .Returns(new AuditEventId
            {
                EventId = "evt-1",
                EventHash = "hash",
                SequenceNumber = 1,
                RecordedAt = DateTimeOffset.UtcNow
            });

        await sut.StoreAsync(auditEvent, CancellationToken.None);

        capturedEvent.ShouldNotBeNull();
        capturedEvent.ActorId.ShouldNotBe("user-1");
        capturedEvent.IpAddress.ShouldNotBe("10.0.0.1");
        capturedEvent.Reason.ShouldNotBe("test-reason");
        capturedEvent.UserAgent.ShouldNotBe("TestAgent/1.0");
    }

    [Fact]
    public async Task Decrypt_fields_on_get_by_id()
    {
        SetupEncryption();
        var sut = CreateSut();

        // Setup inner store to return an event with "encrypted" fields
        var encryptedActorId = await EncryptString("original-user");
        var encryptedIp = await EncryptString("10.0.0.1");

        A.CallTo(() => _innerStore.GetByIdAsync("evt-1", A<CancellationToken>._))
            .Returns(new AuditEvent
            {
                EventId = "evt-1",
                EventType = AuditEventType.DataAccess,
                Action = "Read",
                Outcome = AuditOutcome.Success,
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = encryptedActorId,
                IpAddress = encryptedIp
            });

        var result = await sut.GetByIdAsync("evt-1", CancellationToken.None);

        result.ShouldNotBeNull();
        result.ActorId.ShouldBe("original-user");
        result.IpAddress.ShouldBe("10.0.0.1");
    }

    [Fact]
    public async Task Return_null_from_get_by_id_when_not_found()
    {
        var sut = CreateSut();

        A.CallTo(() => _innerStore.GetByIdAsync("evt-missing", A<CancellationToken>._))
            .Returns(Task.FromResult<AuditEvent?>(null));

        var result = await sut.GetByIdAsync("evt-missing", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Decrypt_fields_on_query()
    {
        SetupEncryption();
        var sut = CreateSut();

        var encryptedActorId = await EncryptString("queried-user");

        A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AuditEvent>>(
            [
                new AuditEvent
                {
                    EventId = "evt-1",
                    EventType = AuditEventType.DataAccess,
                    Action = "Read",
                    Outcome = AuditOutcome.Success,
                    Timestamp = DateTimeOffset.UtcNow,
                    ActorId = encryptedActorId
                }
            ]));

        var results = await sut.QueryAsync(new AuditQuery(), CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].ActorId.ShouldBe("queried-user");
    }

    [Fact]
    public async Task Return_empty_list_from_query_when_no_results()
    {
        var sut = CreateSut();

        A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

        var results = await sut.QueryAsync(new AuditQuery(), CancellationToken.None);

        results.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Delegate_count_to_inner_store()
    {
        var sut = CreateSut();
        var query = new AuditQuery();

        A.CallTo(() => _innerStore.CountAsync(query, A<CancellationToken>._))
            .Returns(42L);

        var count = await sut.CountAsync(query, CancellationToken.None);

        count.ShouldBe(42);
    }

    [Fact]
    public async Task Delegate_verify_chain_integrity_to_inner_store()
    {
        var sut = CreateSut();
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow;
        var expected = AuditIntegrityResult.Valid(10, start, end);

        A.CallTo(() => _innerStore.VerifyChainIntegrityAsync(start, end, A<CancellationToken>._))
            .Returns(expected);

        var result = await sut.VerifyChainIntegrityAsync(start, end, CancellationToken.None);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Decrypt_fields_on_get_last_event()
    {
        SetupEncryption();
        var sut = CreateSut();

        var encryptedActorId = await EncryptString("last-user");

        A.CallTo(() => _innerStore.GetLastEventAsync("tenant-1", A<CancellationToken>._))
            .Returns(new AuditEvent
            {
                EventId = "evt-last",
                EventType = AuditEventType.DataAccess,
                Action = "Read",
                Outcome = AuditOutcome.Success,
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = encryptedActorId
            });

        var result = await sut.GetLastEventAsync("tenant-1", CancellationToken.None);

        result.ShouldNotBeNull();
        result.ActorId.ShouldBe("last-user");
    }

    [Fact]
    public async Task Return_null_from_get_last_event_when_not_found()
    {
        var sut = CreateSut();

        A.CallTo(() => _innerStore.GetLastEventAsync("empty-tenant", A<CancellationToken>._))
            .Returns(Task.FromResult<AuditEvent?>(null));

        var result = await sut.GetLastEventAsync("empty-tenant", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Not_encrypt_null_or_empty_fields()
    {
        SetupEncryption();
        var sut = CreateSut();
        var auditEvent = CreateEvent(ipAddress: null, reason: null, userAgent: null);

        AuditEvent? capturedEvent = null;
        A.CallTo(() => _innerStore.StoreAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent evt, CancellationToken _) => capturedEvent = evt)
            .Returns(new AuditEventId
            {
                EventId = "evt-1",
                EventHash = "hash",
                SequenceNumber = 1,
                RecordedAt = DateTimeOffset.UtcNow
            });

        await sut.StoreAsync(auditEvent, CancellationToken.None);

        capturedEvent.ShouldNotBeNull();
        capturedEvent.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void Throw_argument_null_for_null_inner_store()
    {
        Should.Throw<ArgumentNullException>(() =>
            new EncryptingAuditEventStore(
                null!,
                _encryption,
                Microsoft.Extensions.Options.Options.Create(new AuditEncryptionOptions())));
    }

    [Fact]
    public void Throw_argument_null_for_null_encryption_provider()
    {
        Should.Throw<ArgumentNullException>(() =>
            new EncryptingAuditEventStore(
                _innerStore,
                null!,
                Microsoft.Extensions.Options.Options.Create(new AuditEncryptionOptions())));
    }

    [Fact]
    public void Throw_argument_null_for_null_options()
    {
        Should.Throw<ArgumentNullException>(() =>
            new EncryptingAuditEventStore(
                _innerStore,
                _encryption,
                null!));
    }

    [Fact]
    public async Task Throw_argument_null_for_null_event_in_store()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.StoreAsync(null!, CancellationToken.None));
    }

    private static Task<string> EncryptString(string plaintext)
    {
        // Simulate the encryption format used by EncryptingAuditEventStore
        // Uses camelCase property naming to match AuditEncryptionJsonContext
        var encrypted = new EncryptedData
        {
            Ciphertext = Encoding.UTF8.GetBytes(plaintext),
            KeyId = "key-1",
            KeyVersion = 1,
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            Iv = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(encrypted, jsonOptions);
        return Task.FromResult(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }
}
