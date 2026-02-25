using Excalibur.Dispatch.AuditLogging.Retention;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Tests.Retention;

public class DefaultAuditRetentionServiceShould
{
    private readonly IAuditStore _fakeStore = A.Fake<IAuditStore>();

    private DefaultAuditRetentionService CreateSut(AuditRetentionOptions? options = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? new AuditRetentionOptions());
        return new DefaultAuditRetentionService(
            _fakeStore,
            opts,
            NullLogger<DefaultAuditRetentionService>.Instance);
    }

    [Fact]
    public async Task Enforce_retention_queries_expired_events()
    {
        var sut = CreateSut(new AuditRetentionOptions
        {
            RetentionPeriod = TimeSpan.FromDays(30),
            BatchSize = 100
        });

        A.CallTo(() => _fakeStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

        await sut.EnforceRetentionAsync(CancellationToken.None);

        A.CallTo(() => _fakeStore.QueryAsync(
                A<AuditQuery>.That.Matches(q =>
                    q.MaxResults == 100 &&
                    q.OrderByDescending == false &&
                    q.EndDate.HasValue),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Return_early_when_no_expired_events()
    {
        var sut = CreateSut();

        A.CallTo(() => _fakeStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

        await sut.EnforceRetentionAsync(CancellationToken.None);

        // Should have queried but not called anything beyond the query
        A.CallTo(() => _fakeStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
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
