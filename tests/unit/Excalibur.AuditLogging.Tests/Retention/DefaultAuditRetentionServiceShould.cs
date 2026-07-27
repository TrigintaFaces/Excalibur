using Excalibur.AuditLogging.Retention;
using Excalibur.Compliance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Tests.Retention;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DefaultAuditRetentionServiceShould
{
    private readonly IAuditStore _fakeStore = A.Fake<IAuditStore>();

    private DefaultAuditRetentionService CreateSut(
        AuditRetentionOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? new AuditRetentionOptions());
        return new DefaultAuditRetentionService(
            _fakeStore,
            opts,
            NullLogger<DefaultAuditRetentionService>.Instance,
            timeProvider);
    }

    /// <summary>
    /// A clock that does not move, so a cutoff derived from it can be asserted EXACTLY.
    /// </summary>
    /// <remarks>
    /// The cutoff arm below used to bound the value in a one-day window because the service read the system
    /// clock internally. That window was not merely imprecise — it was <b>wrong in one direction</b>: the
    /// service computes <c>UtcNow - period</c> <i>after</i> the test captures its reference instant, so the
    /// real cutoff is always slightly LATER than <c>reference - period</c> while the predicate demanded it be
    /// earlier or equal. It passed only while the clock happened not to tick between the two statements.
    /// The service takes an injectable <see cref="TimeProvider"/>, so the range can be replaced by equality.
    /// </remarks>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// Retention must DELETE, and it must delete at the configured cutoff.
    /// </summary>
    /// <remarks>
    /// Rebound from the previous arms, which asserted that retention <c>QueryAsync</c>-ed the expired events.
    /// That is what the service used to do — query them, log that deletion had completed, and delete nothing.
    /// An arm asserting the query therefore passed against a service that removed no data at all: it described
    /// the defect rather than the contract. The service now resolves <see cref="IAuditPurgeCapability"/> and
    /// purges; asserting the query would today assert a call that no longer exists.
    /// </remarks>
    [Fact]
    public async Task Enforce_retention_purges_at_the_configured_cutoff()
    {
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var sut = CreateSut(
            new AuditRetentionOptions
            {
                RetentionPeriod = TimeSpan.FromDays(30),
                BatchSize = 100
            },
            new FixedTimeProvider(now));

        var purge = A.Fake<IAuditPurgeCapability>();
        A.CallTo(() => _fakeStore.GetService(typeof(IAuditPurgeCapability))).Returns(purge);
        A.CallTo(() => purge.PurgeExpiredAsync(A<DateTimeOffset>._, A<CancellationToken>._))
            .Returns(Task.FromResult(0));

        await sut.EnforceRetentionAsync(CancellationToken.None);

        // EXACT, not a window. With the clock injected the cutoff is fully determined, so this pins the
        // property outright: retention deletes at precisely the configured period back. A drifting or
        // zeroed cutoff — the failure that would silently purge everything, or nothing — cannot slip
        // through an equality the way it could through a one-day range.
        A.CallTo(() => purge.PurgeExpiredAsync(
                now - TimeSpan.FromDays(30),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    /// <summary>
    /// A store with no purge capability must not be treated as a successful retention run.
    /// </summary>
    /// <remarks>
    /// This is the non-target half: the service must not report that retention happened when it had no way to
    /// delete anything. That exact shape — reporting completion while deleting nothing — is the defect this
    /// service was repaired for, and nothing else here would notice its return.
    /// </remarks>
    [Fact]
    public async Task Not_purge_when_the_store_provides_no_purge_capability()
    {
        var sut = CreateSut();

        A.CallTo(() => _fakeStore.GetService(typeof(IAuditPurgeCapability))).Returns(null);

        var ex = await Should.ThrowAsync<NotSupportedException>(
            () => sut.EnforceRetentionAsync(CancellationToken.None));

        ex.Message.ShouldContain(
            "does not",
            Case.Insensitive,
            "the failure must name what is missing, so an operator can act rather than guess.");
    }

    [Fact]
    public async Task Get_retention_policy_returns_configured_values()
    {
        var options = new AuditRetentionOptions
        {
            RetentionPeriod = TimeSpan.FromDays(365),
            CleanupInterval = TimeSpan.FromHours(6),
            BatchSize = 5000,
            ArchiveBeforeDelete = true
        };
        var sut = CreateSut(options);

        var policy = await sut.GetRetentionPolicyAsync(CancellationToken.None);

        policy.RetentionPeriod.ShouldBe(TimeSpan.FromDays(365));
        policy.CleanupInterval.ShouldBe(TimeSpan.FromHours(6));
        policy.BatchSize.ShouldBe(5000);
        policy.ArchiveBeforeDelete.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_retention_policy_returns_default_values()
    {
        var sut = CreateSut();

        var policy = await sut.GetRetentionPolicyAsync(CancellationToken.None);

        policy.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7 * 365));
        policy.CleanupInterval.ShouldBe(TimeSpan.FromDays(1));
        policy.BatchSize.ShouldBe(10000);
        policy.ArchiveBeforeDelete.ShouldBeFalse();
    }

    [Fact]
    public void Throw_argument_null_for_null_store()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditRetentionService(
                null!,
                Microsoft.Extensions.Options.Options.Create(new AuditRetentionOptions()),
                NullLogger<DefaultAuditRetentionService>.Instance));
    }

    [Fact]
    public void Throw_argument_null_for_null_options()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditRetentionService(
                _fakeStore,
                null!,
                NullLogger<DefaultAuditRetentionService>.Instance));
    }

    [Fact]
    public void Throw_argument_null_for_null_logger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditRetentionService(
                _fakeStore,
                Microsoft.Extensions.Options.Options.Create(new AuditRetentionOptions()),
                null!));
    }
}
