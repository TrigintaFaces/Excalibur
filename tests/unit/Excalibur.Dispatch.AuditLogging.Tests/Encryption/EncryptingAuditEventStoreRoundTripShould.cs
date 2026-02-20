using System.Text;

using Excalibur.Dispatch.AuditLogging.Encryption;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Tests.Encryption;

/// <summary>
/// Integration-style tests verifying the full encrypt/store/retrieve/decrypt round-trip
/// using InMemoryAuditStore and a fake IEncryptionProvider.
/// </summary>
public class EncryptingAuditEventStoreRoundTripShould
{
    private readonly InMemoryAuditStore _innerStore = new();
    private readonly IEncryptionProvider _encryption = A.Fake<IEncryptionProvider>();

    public EncryptingAuditEventStoreRoundTripShould()
    {
        // Simple XOR-style fake encryption for round-trip testing
        A.CallTo(() => _encryption.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
            .ReturnsLazily((byte[] plaintext, EncryptionContext _, CancellationToken _) =>
            {
                // Simple reversible transformation for testing
                var ciphertext = new byte[plaintext.Length];
                for (var i = 0; i < plaintext.Length; i++)
                {
                    ciphertext[i] = (byte)(plaintext[i] ^ 0x42);
                }

                return Task.FromResult(new EncryptedData
                {
                    Ciphertext = ciphertext,
                    KeyId = "test-key",
                    KeyVersion = 1,
                    Algorithm = EncryptionAlgorithm.Aes256Gcm,
                    Iv = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]
                });
            });

        A.CallTo(() => _encryption.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
            .ReturnsLazily((EncryptedData data, EncryptionContext _, CancellationToken _) =>
            {
                // Reverse the XOR transformation
                var plaintext = new byte[data.Ciphertext.Length];
                for (var i = 0; i < data.Ciphertext.Length; i++)
                {
                    plaintext[i] = (byte)(data.Ciphertext[i] ^ 0x42);
                }

                return Task.FromResult(plaintext);
            });
    }

    private EncryptingAuditEventStore CreateSut(AuditEncryptionOptions? options = null) =>
        new(
            _innerStore,
            _encryption,
            Microsoft.Extensions.Options.Options.Create(options ?? new AuditEncryptionOptions()));

    [Fact]
    public async Task Round_trip_actor_id_through_encrypt_store_decrypt()
    {
        var sut = CreateSut();
        var original = new AuditEvent
        {
            EventId = "evt-rt1",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "sensitive-user@example.com",
            IpAddress = "192.168.1.100"
        };

        await sut.StoreAsync(original, CancellationToken.None);

        var retrieved = await sut.GetByIdAsync("evt-rt1", CancellationToken.None);

        retrieved.ShouldNotBeNull();
        retrieved.ActorId.ShouldBe("sensitive-user@example.com");
        retrieved.IpAddress.ShouldBe("192.168.1.100");
    }

    [Fact]
    public async Task Round_trip_all_encrypted_fields()
    {
        var options = new AuditEncryptionOptions
        {
            EncryptActorId = true,
            EncryptIpAddress = true,
            EncryptReason = true,
            EncryptUserAgent = true
        };
        var sut = CreateSut(options);

        var original = new AuditEvent
        {
            EventId = "evt-rt2",
            EventType = AuditEventType.Security,
            Action = "KeyRotation",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "admin@corp.com",
            IpAddress = "10.0.0.1",
            Reason = "Scheduled rotation",
            UserAgent = "AdminTool/2.0"
        };

        await sut.StoreAsync(original, CancellationToken.None);
        var retrieved = await sut.GetByIdAsync("evt-rt2", CancellationToken.None);

        retrieved.ShouldNotBeNull();
        retrieved.ActorId.ShouldBe("admin@corp.com");
        retrieved.IpAddress.ShouldBe("10.0.0.1");
        retrieved.Reason.ShouldBe("Scheduled rotation");
        retrieved.UserAgent.ShouldBe("AdminTool/2.0");
    }

    [Fact]
    public async Task Preserve_non_encrypted_fields()
    {
        var sut = CreateSut();
        var timestamp = DateTimeOffset.UtcNow;

        var original = new AuditEvent
        {
            EventId = "evt-rt3",
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Failure,
            Timestamp = timestamp,
            ActorId = "user-1",
            ResourceId = "login-page",
            ResourceType = "Page",
            CorrelationId = "corr-123",
            SessionId = "sess-456"
        };

        await sut.StoreAsync(original, CancellationToken.None);
        var retrieved = await sut.GetByIdAsync("evt-rt3", CancellationToken.None);

        retrieved.ShouldNotBeNull();
        retrieved.EventType.ShouldBe(AuditEventType.Authentication);
        retrieved.Action.ShouldBe("Login");
        retrieved.Outcome.ShouldBe(AuditOutcome.Failure);
        retrieved.ResourceId.ShouldBe("login-page");
        retrieved.ResourceType.ShouldBe("Page");
        retrieved.CorrelationId.ShouldBe("corr-123");
        retrieved.SessionId.ShouldBe("sess-456");
    }

    [Fact]
    public async Task Round_trip_query_with_decryption()
    {
        var sut = CreateSut();

        for (var i = 0; i < 3; i++)
        {
            await sut.StoreAsync(new AuditEvent
            {
                EventId = $"evt-rq{i}",
                EventType = AuditEventType.DataAccess,
                Action = "Read",
                Outcome = AuditOutcome.Success,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(i),
                ActorId = $"user-{i}",
                IpAddress = $"10.0.0.{i}"
            }, CancellationToken.None);
        }

        var results = await sut.QueryAsync(new AuditQuery(), CancellationToken.None);

        results.Count.ShouldBe(3);
        for (var i = 0; i < 3; i++)
        {
            var evt = results.First(e => e.EventId == $"evt-rq{i}");
            evt.ActorId.ShouldBe($"user-{i}");
            evt.IpAddress.ShouldBe($"10.0.0.{i}");
        }
    }

    [Fact]
    public async Task Round_trip_get_last_event_with_decryption()
    {
        var sut = CreateSut();

        await sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-last-rt",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "last-actor@company.com",
            IpAddress = "172.16.0.1"
        }, CancellationToken.None);

        var result = await sut.GetLastEventAsync(null, CancellationToken.None);

        result.ShouldNotBeNull();
        result.ActorId.ShouldBe("last-actor@company.com");
        result.IpAddress.ShouldBe("172.16.0.1");
    }
}
