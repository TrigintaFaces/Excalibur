using Excalibur.AuditLogging.Encryption;
using Excalibur.Compliance;

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Tests.Encryption;

/// <summary>
/// Binds the decorator's behaviour on a filter over a field it encrypts.
/// </summary>
/// <remarks>
/// <para>
/// The defect these arms exist for was silent, and that is what makes the negative arms the load-bearing
/// half. The decorator encrypts the actor id at rest with a randomized cipher, then forwarded a query
/// comparing the caller's plaintext against that ciphertext; the comparison matched nothing, the call
/// SUCCEEDED, and the caller received an empty list. An operator asking what an actor did was answered
/// "nothing" while the records sat present and unmatchable -- the worst answer an audit trail can give,
/// because it is indistinguishable from the truth.
/// </para>
/// <para>
/// So a fix that merely stopped returning the wrong rows would still pass a test that only checked the
/// servable case. Both halves are asserted here: a filter the decorator CAN serve reaches the store and
/// filters (so the guard does not over-refuse and quietly disable querying), and a filter it CANNOT serve
/// throws before the store is asked (so no path remains on which an empty answer can be manufactured).
/// </para>
/// <para>
/// Every field with this shape is covered, not only the one the failure named. Two fields are both
/// encryptable and filterable -- ActorId and IpAddress -- and each is asserted across both read members,
/// because a count that answered zero would be the more dangerous of the two: a zero carries no hint that
/// anything was withheld.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingAuditEventStoreFilterRefusalShould : IDisposable
{
    private readonly InMemoryAuditStore _realStore =
        new(AuditIntegrityTestStrategy.Create(), TestTenantHosts.UntenantedAuditHost());

    private readonly IAuditStore _fakeStore = A.Fake<IAuditStore>();
    private readonly IEncryptionProvider _encryption = A.Fake<IEncryptionProvider>();

    public EncryptingAuditEventStoreFilterRefusalShould()
    {
        // A reversible stand-in for the cipher. These arms are about which queries the decorator will
        // ACCEPT, not about the strength of the transform; a real AES-256-GCM round-trip is bound against
        // real Postgres by the conformance suite.
        A.CallTo(() => _encryption.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
            .ReturnsLazily((byte[] plaintext, EncryptionContext _, CancellationToken _) =>
                Task.FromResult(new EncryptedData
                {
                    Ciphertext = plaintext,
                    KeyId = "key-1",
                    KeyVersion = 1,
                    Algorithm = EncryptionAlgorithm.Aes256Gcm,
                    Iv = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
                }));

        A.CallTo(() => _encryption.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
            .ReturnsLazily((EncryptedData data, EncryptionContext _, CancellationToken _) =>
                Task.FromResult(data.Ciphertext));
    }

    public void Dispose() => _realStore.Dispose();

    private EncryptingAuditEventStore Over(IAuditStore inner, AuditEncryptionOptions options) =>
        new(inner, _encryption, Options.Create(options));

    private static AuditEvent Event(string actorId, string? ipAddress = null) =>
        new()
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = actorId,
            IpAddress = ipAddress,
        };

    // ---- The servable half: a field left in the clear stays queryable, end to end. ----

    [Fact]
    public async Task Serve_an_actor_id_filter_when_the_actor_id_is_left_in_the_clear()
    {
        var sut = Over(_realStore, new AuditEncryptionOptions { EncryptActorId = false, EncryptIpAddress = false });

        var wanted = Event("actor-1");
        var other = Event("actor-2");
        _ = await sut.StoreAsync(wanted, CancellationToken.None);
        _ = await sut.StoreAsync(other, CancellationToken.None);

        var results = await sut.QueryAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None);

        results.ShouldContain(e => e.EventId == wanted.EventId);
        results.ShouldNotContain(e => e.EventId == other.EventId);
    }

    [Fact]
    public async Task Serve_an_ip_address_filter_when_the_address_is_left_in_the_clear()
    {
        var sut = Over(_realStore, new AuditEncryptionOptions { EncryptActorId = false, EncryptIpAddress = false });

        var wanted = Event("actor-1", "10.0.0.1");
        var other = Event("actor-2", "10.0.0.2");
        _ = await sut.StoreAsync(wanted, CancellationToken.None);
        _ = await sut.StoreAsync(other, CancellationToken.None);

        var results = await sut.QueryAsync(new AuditQuery { IpAddress = "10.0.0.1" }, CancellationToken.None);

        results.ShouldContain(e => e.EventId == wanted.EventId);
        results.ShouldNotContain(e => e.EventId == other.EventId);
    }

    [Fact]
    public async Task Count_an_actor_id_filter_when_the_actor_id_is_left_in_the_clear()
    {
        var sut = Over(_realStore, new AuditEncryptionOptions { EncryptActorId = false, EncryptIpAddress = false });

        _ = await sut.StoreAsync(Event("actor-1"), CancellationToken.None);
        _ = await sut.StoreAsync(Event("actor-1"), CancellationToken.None);
        _ = await sut.StoreAsync(Event("actor-2"), CancellationToken.None);

        var count = await sut.CountAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task Forward_a_query_that_names_no_encrypted_field_even_with_encryption_on()
    {
        // The guard must refuse the fields it cannot serve and NOTHING else. A guard that tripped on any
        // filter would turn the whole store off while every arm above still passed.
        var sut = Over(_realStore, new AuditEncryptionOptions());

        var authentication = Event("actor-1") with { EventType = AuditEventType.Authentication };
        _ = await sut.StoreAsync(authentication, CancellationToken.None);
        _ = await sut.StoreAsync(Event("actor-2"), CancellationToken.None);

        var results = await sut.QueryAsync(
            new AuditQuery { EventTypes = [AuditEventType.Authentication] },
            CancellationToken.None);

        results.ShouldContain(e => e.EventId == authentication.EventId);
        results.Count.ShouldBe(1);
    }

    // ---- The unservable half: refused loudly, before the store is asked. ----

    [Fact]
    public async Task Refuse_an_actor_id_filter_when_the_actor_id_is_encrypted()
    {
        var sut = Over(_fakeStore, new AuditEncryptionOptions { EncryptActorId = true });

        var thrown = await Should.ThrowAsync<NotSupportedException>(
            () => sut.QueryAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None));

        thrown.Message.ShouldContain(nameof(AuditQuery.ActorId));
        thrown.Message.ShouldContain(nameof(AuditEncryptionOptions.EncryptActorId));
    }

    [Fact]
    public async Task Refuse_an_ip_address_filter_when_the_address_is_encrypted()
    {
        var sut = Over(_fakeStore, new AuditEncryptionOptions { EncryptIpAddress = true });

        var thrown = await Should.ThrowAsync<NotSupportedException>(
            () => sut.QueryAsync(new AuditQuery { IpAddress = "10.0.0.1" }, CancellationToken.None));

        thrown.Message.ShouldContain(nameof(AuditQuery.IpAddress));
        thrown.Message.ShouldContain(nameof(AuditEncryptionOptions.EncryptIpAddress));
    }

    [Fact]
    public async Task Refuse_a_count_over_an_encrypted_actor_id_rather_than_answering_zero()
    {
        var sut = Over(_fakeStore, new AuditEncryptionOptions { EncryptActorId = true });

        var thrown = await Should.ThrowAsync<NotSupportedException>(
            () => sut.CountAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None));

        thrown.Message.ShouldContain(nameof(AuditQuery.ActorId));
    }

    [Fact]
    public async Task Refuse_a_count_over_an_encrypted_ip_address_rather_than_answering_zero()
    {
        var sut = Over(_fakeStore, new AuditEncryptionOptions { EncryptIpAddress = true });

        var thrown = await Should.ThrowAsync<NotSupportedException>(
            () => sut.CountAsync(new AuditQuery { IpAddress = "10.0.0.1" }, CancellationToken.None));

        thrown.Message.ShouldContain(nameof(AuditQuery.IpAddress));
    }

    [Fact]
    public async Task Refuse_before_the_inner_store_is_asked_so_no_empty_answer_can_be_produced()
    {
        // This is the arm that distinguishes the fix from a cosmetic one. Delegating and then throwing
        // would still be a query the database ran; refusing on the way IN means there is no code path on
        // which an empty result set exists to be returned, cached, or logged as a legitimate answer.
        var sut = Over(_fakeStore, new AuditEncryptionOptions());

        _ = await Should.ThrowAsync<NotSupportedException>(
            () => sut.QueryAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None));
        _ = await Should.ThrowAsync<NotSupportedException>(
            () => sut.CountAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None));

        A.CallTo(() => _fakeStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Refuse_under_the_shipped_defaults_because_that_is_the_configuration_consumers_get()
    {
        // AuditEncryptionOptions encrypts ActorId and IpAddress out of the box. An arm that only proved
        // the refusal under a hand-built configuration would leave the default -- the one every consumer
        // runs -- unasserted.
        var sut = Over(_fakeStore, new AuditEncryptionOptions());

        _ = await Should.ThrowAsync<NotSupportedException>(
            () => sut.QueryAsync(new AuditQuery { ActorId = "actor-1" }, CancellationToken.None));
        _ = await Should.ThrowAsync<NotSupportedException>(
            () => sut.QueryAsync(new AuditQuery { IpAddress = "10.0.0.1" }, CancellationToken.None));
    }
}
