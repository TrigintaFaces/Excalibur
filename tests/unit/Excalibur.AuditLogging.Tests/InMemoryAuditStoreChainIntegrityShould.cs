using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Compliance;

using Excalibur.AuditLogging;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Tests specifically for hash chain integrity verification in InMemoryAuditStore.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryAuditStoreChainIntegrityShould : IDisposable
{
    private readonly InMemoryAuditStore _sut = new(AuditIntegrityTestStrategy.Create());
    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task Verify_single_event_chain()
    {
        var now = DateTimeOffset.UtcNow;
        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-1",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = now,
            ActorId = "user-1"
        }, CancellationToken.None);

        var result = await _sut.VerifyChainIntegrityAsync(
            now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.EventsVerified.ShouldBe(1);
    }

    [Fact]
    public async Task Verify_multi_event_chain()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++)
        {
            await _sut.StoreAsync(new AuditEvent
            {
                EventId = $"evt-{i}",
                EventType = AuditEventType.DataAccess,
                Action = "Read",
                Outcome = AuditOutcome.Success,
                Timestamp = now.AddSeconds(i),
                ActorId = "user-1"
            }, CancellationToken.None);
        }

        var result = await _sut.VerifyChainIntegrityAsync(
            now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.EventsVerified.ShouldBe(10);
    }

    [Fact]
    public async Task Detect_tampered_event_hash()
    {
        var now = DateTimeOffset.UtcNow;
        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-1",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = now,
            ActorId = "user-1"
        }, CancellationToken.None);

        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-2",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = now.AddSeconds(1),
            ActorId = "user-1"
        }, CancellationToken.None);

        // Sanity: the untampered chain verifies (otherwise this test would pass vacuously).
        var before = await _sut.VerifyChainIntegrityAsync(
            now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);
        before.IsValid.ShouldBeTrue("Pre-tamper chain must be valid or the tamper assertion is vacuous.");

        // Tamper the SECOND stored event in place: mutate its canonical content (Action) while KEEPING its
        // original keyed-MAC (EventHash). Verification recomputes the MAC over the canonicalized event, so the
        // stored tag no longer matches the tampered content -> the chain must be reported invalid at evt-2.
        TamperStoredEventAction("evt-2", "Read-TAMPERED");

        var result = await _sut.VerifyChainIntegrityAsync(
            now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);

        result.IsValid.ShouldBeFalse(
            "A stored event whose content was altered after its keyed-MAC was computed MUST fail chain verification.");
        result.EventsVerified.ShouldBe(2);
        result.FirstViolationEventId.ShouldBe(
            "evt-2",
            "The first (and only) tampered event is evt-2; verification reports it as the first violation.");
        result.ViolationCount.ShouldBe(1);
        result.ViolationDescription.ShouldNotBeNullOrWhiteSpace();
    }

    // Reaches the store's private event map and replaces one stored event with a content-mutated copy that
    // retains the original (now-stale) EventHash/PreviousEventHash, simulating post-write tampering. Reflection
    // (not widened production visibility) per the internal-first rule; throws rather than passing vacuously if
    // the field/event can't be located (the store shape changed -> update this guard).
    private void TamperStoredEventAction(string eventId, string tamperedAction)
    {
        var field = typeof(InMemoryAuditStore).GetField("_eventsById", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "fq9x3u — InMemoryAuditStore._eventsById not found; the store's storage shape changed — update this tamper guard.");

        var map = (ConcurrentDictionary<string, AuditEvent>?)field.GetValue(_sut)
            ?? throw new InvalidOperationException("fq9x3u — _eventsById was null.");

        var key = map.FirstOrDefault(kvp => string.Equals(kvp.Value.EventId, eventId, StringComparison.Ordinal)).Key
            ?? throw new InvalidOperationException(
                $"fq9x3u — stored event '{eventId}' not found in _eventsById; cannot tamper (refusing to pass vacuously).");

        var stored = map[key];
        stored.Action.ShouldNotBe(tamperedAction, "The tampered Action must differ from the original.");

        // Keep EventHash + PreviousEventHash; change only the canonical content -> stored MAC becomes stale.
        map[key] = stored with { Action = tamperedAction };
    }

    [Fact]
    public async Task Return_date_range_in_result()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-7);
        var end = DateTimeOffset.UtcNow;

        var result = await _sut.VerifyChainIntegrityAsync(
            start, end, CancellationToken.None);

        result.StartDate.ShouldBe(start);
        result.EndDate.ShouldBe(end);
    }

    [Fact]
    public async Task Only_verify_events_in_date_range()
    {
        var now = DateTimeOffset.UtcNow;

        // Store events outside the range
        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-old",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = now.AddDays(-30),
            ActorId = "user-1"
        }, CancellationToken.None);

        await _sut.StoreAsync(new AuditEvent
        {
            EventId = "evt-in-range",
            EventType = AuditEventType.DataAccess,
            Action = "Read",
            Outcome = AuditOutcome.Success,
            Timestamp = now,
            ActorId = "user-1"
        }, CancellationToken.None);

        var result = await _sut.VerifyChainIntegrityAsync(
            now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);

        result.EventsVerified.ShouldBe(1);
    }

    [Fact]
    public async Task Respect_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.VerifyChainIntegrityAsync(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow,
                cts.Token));
    }
}
