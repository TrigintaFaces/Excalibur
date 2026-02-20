using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging.Tests;

public class AuditHasherShould
{
    private static AuditEvent CreateEvent(string eventId = "evt-1", string action = "Read", string actorId = "user-1") =>
        new()
        {
            EventId = eventId,
            EventType = AuditEventType.DataAccess,
            Action = action,
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = actorId
        };

    [Fact]
    public void Compute_hash_for_valid_audit_event()
    {
        var auditEvent = CreateEvent();

        var hash = AuditHasher.ComputeHash(auditEvent, null);

        hash.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Produce_consistent_hash_for_same_input()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            EventId = "evt-consistent",
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Success,
            Timestamp = timestamp,
            ActorId = "user-42"
        };

        var hash1 = AuditHasher.ComputeHash(auditEvent, "prev-hash");
        var hash2 = AuditHasher.ComputeHash(auditEvent, "prev-hash");

        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void Produce_different_hash_with_different_previous_hash()
    {
        var auditEvent = CreateEvent();

        var hash1 = AuditHasher.ComputeHash(auditEvent, "prev-hash-1");
        var hash2 = AuditHasher.ComputeHash(auditEvent, "prev-hash-2");

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void Produce_different_hash_for_null_vs_non_null_previous_hash()
    {
        var auditEvent = CreateEvent();

        var hash1 = AuditHasher.ComputeHash(auditEvent, null);
        var hash2 = AuditHasher.ComputeHash(auditEvent, "some-hash");

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void Throw_argument_null_exception_when_audit_event_is_null()
    {
        Should.Throw<ArgumentNullException>(() => AuditHasher.ComputeHash(null!, null));
    }

    [Fact]
    public void Include_metadata_in_hash_computation()
    {
        var metadata1 = new Dictionary<string, string> { ["key1"] = "value1" };
        var metadata2 = new Dictionary<string, string> { ["key1"] = "value2" };

        var timestamp = DateTimeOffset.UtcNow;
        var evt1 = new AuditEvent
        {
            EventId = "evt-meta",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = timestamp,
            ActorId = "user-1",
            Metadata = metadata1
        };
        var evt2 = evt1 with { Metadata = metadata2 };

        var hash1 = AuditHasher.ComputeHash(evt1, null);
        var hash2 = AuditHasher.ComputeHash(evt2, null);

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void Produce_base64_encoded_hash()
    {
        var auditEvent = CreateEvent();

        var hash = AuditHasher.ComputeHash(auditEvent, null);

        // SHA-256 produces 32 bytes, Base64 of 32 bytes is 44 characters
        hash.Length.ShouldBe(44);
        Should.NotThrow(() => Convert.FromBase64String(hash));
    }

    [Fact]
    public void Verify_hash_returns_true_for_valid_hash()
    {
        var auditEvent = CreateEvent();
        var previousHash = "some-previous-hash";
        var computedHash = AuditHasher.ComputeHash(auditEvent, previousHash);

        var eventWithHash = auditEvent with { EventHash = computedHash };

        AuditHasher.VerifyHash(eventWithHash, previousHash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_hash_returns_false_for_tampered_hash()
    {
        var auditEvent = CreateEvent();
        var eventWithHash = auditEvent with { EventHash = "tampered-hash-value" };

        AuditHasher.VerifyHash(eventWithHash, null).ShouldBeFalse();
    }

    [Fact]
    public void Verify_hash_returns_false_for_null_event()
    {
        AuditHasher.VerifyHash(null!, null).ShouldBeFalse();
    }

    [Fact]
    public void Verify_hash_returns_false_for_null_event_hash()
    {
        var auditEvent = CreateEvent();

        AuditHasher.VerifyHash(auditEvent, null).ShouldBeFalse();
    }

    [Fact]
    public void Compute_genesis_hash_returns_non_empty_string()
    {
        var chainInitTime = DateTimeOffset.UtcNow;

        var hash = AuditHasher.ComputeGenesisHash(null, chainInitTime);

        hash.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Compute_genesis_hash_uses_default_when_tenant_is_null()
    {
        var chainInitTime = DateTimeOffset.UtcNow;

        var hash1 = AuditHasher.ComputeGenesisHash(null, chainInitTime);
        var hash2 = AuditHasher.ComputeGenesisHash(null, chainInitTime);

        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void Compute_genesis_hash_differs_per_tenant()
    {
        var chainInitTime = DateTimeOffset.UtcNow;

        var hash1 = AuditHasher.ComputeGenesisHash("tenant-a", chainInitTime);
        var hash2 = AuditHasher.ComputeGenesisHash("tenant-b", chainInitTime);

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void Compute_genesis_hash_differs_per_init_time()
    {
        var time1 = DateTimeOffset.UtcNow;
        var time2 = time1.AddSeconds(1);

        var hash1 = AuditHasher.ComputeGenesisHash("tenant", time1);
        var hash2 = AuditHasher.ComputeGenesisHash("tenant", time2);

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void Produce_different_hashes_for_different_event_types()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evt1 = new AuditEvent
        {
            EventId = "evt-type",
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Success,
            Timestamp = timestamp,
            ActorId = "user-1"
        };
        var evt2 = evt1 with { EventType = AuditEventType.Authorization };

        var hash1 = AuditHasher.ComputeHash(evt1, null);
        var hash2 = AuditHasher.ComputeHash(evt2, null);

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void Sort_metadata_keys_deterministically()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var metadata1 = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };
        var metadata2 = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };

        var evt1 = new AuditEvent
        {
            EventId = "evt-sort",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = timestamp,
            ActorId = "user-1",
            Metadata = metadata1
        };
        var evt2 = evt1 with { Metadata = metadata2 };

        var hash1 = AuditHasher.ComputeHash(evt1, null);
        var hash2 = AuditHasher.ComputeHash(evt2, null);

        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void Handle_event_with_all_optional_fields_populated()
    {
        var auditEvent = new AuditEvent
        {
            EventId = "evt-full",
            EventType = AuditEventType.Security,
            Action = "KeyRotation",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "admin-1",
            ActorType = "Administrator",
            ResourceId = "key-123",
            ResourceType = "EncryptionKey",
            ResourceClassification = DataClassification.Restricted,
            TenantId = "tenant-abc",
            CorrelationId = "corr-456",
            SessionId = "sess-789",
            IpAddress = "192.168.1.1",
            UserAgent = "TestClient/1.0",
            Reason = "Scheduled key rotation",
            Metadata = new Dictionary<string, string> { ["detail"] = "AES-256" }
        };

        var hash = AuditHasher.ComputeHash(auditEvent, "prev-hash");

        hash.ShouldNotBeNullOrWhiteSpace();
        hash.Length.ShouldBe(44);
    }
}
