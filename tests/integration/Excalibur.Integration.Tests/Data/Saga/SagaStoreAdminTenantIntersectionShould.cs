// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Postgres;
using Excalibur.Saga.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Author≠impl real-infra regression lock for the saga admin read-model <em>tenant-intersection</em>
/// property closed by <c>k6kx2j</c> across the SQL providers (SQL Server, PostgreSQL, Oracle): the ambient
/// <see cref="ITenantContext"/> scope is <strong>intersected</strong> with the caller-supplied
/// <see cref="SagaQueryFilter.TenantId"/> in <c>QuerySagaSummariesRequest</c>
/// (<c>AND (@FilterTenantId IS NULL OR tenant_id = @FilterTenantId) AND tenant_id = @TenantId</c>), never
/// substituted by it — so a scoped caller supplying <em>another</em> tenant's filter id gets the empty
/// intersection, not that tenant's rows. Statistics emit the ambient predicate directly.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this lock exists:</b> at HEAD the property the fix closed was asserted <em>nowhere</em> (measured:
/// tests binding <c>FilterTenantId</c> = 0). A future edit reverting the intersect to a substitution would
/// reintroduce a cross-tenant read with a fully green suite. This binds the emitted behaviour, not the SQL.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> runs against a real SQL engine (TestContainers) so the
/// intersected <c>WHERE</c> is evaluated by the engine — a mock cannot reproduce the empty-intersection.
/// NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>). The <em>in-memory</em> saga admin does not model this
/// intersection (its <c>QuerySagasAsync</c> ignores the ambient scope by design), so it is deliberately not a
/// subject of this lock; the property is a SQL-provider guarantee.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — a caller scoped to tenant A supplying tenant B's filter
/// id receives zero rows; LIVENESS — the same scoped caller still receives its <em>own</em> tenant's rows,
/// and an unscoped operator still receives estate-wide statistics. A store that returned nothing to anyone
/// would pass SAFETY but fail LIVENESS.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> restore the substitution (make the caller filter <em>replace</em> the ambient scope,
/// e.g. drop the <c>AND tenant_id = @TenantId</c> ambient term) ⇒ the scoped-A + filter-B query returns
/// tenant B's rows ⇒ the SAFETY fact goes RED.
/// </para>
/// </remarks>
public abstract class SagaStoreAdminTenantIntersectionTestBase
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    /// <summary>Ensures the container is up and the table is clean before a test seeds it.</summary>
    protected abstract Task InitAsync();

    /// <summary>Cleans up the shared table after a test.</summary>
    protected abstract Task CleanupAsync();

    /// <summary>
    /// Creates a store (and its admin projection — the same underlying store) bound to <paramref name="tenantId"/>
    /// via a fixed <see cref="ITenantContext"/>, or unscoped when <paramref name="tenantId"/> is
    /// <see langword="null"/>.
    /// </summary>
    protected abstract (ISagaStore Store, ISagaStoreAdmin Admin) Create(string? tenantId);

    /// <summary>Fixed ambient tenant context test double.</summary>
    protected sealed class FixedTenant(string? tenantId) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;

        public bool HasTenant => TenantId is not null;
    }

    private static TestSagaState NewSaga(bool completed, string? tenantId) => new()
    {
        SagaId = Guid.NewGuid(),
        Completed = completed,
        CompletedAt = completed ? DateTimeOffset.UtcNow : null,
        TenantId = tenantId,
        Data = "seed",
    };

    private async Task SeedTwoTenantsAsync(CancellationToken ct)
    {
        // Seed through a store PER TENANT. The row's tenant comes from the scope the store was
        // constructed with; sagaState.TenantId travels in the payload and does not place the row. This
        // previously seeded through one unscoped store on the belief that the state's own TenantId was
        // the discriminator — which the parameter documentation asserted and the code never did — so
        // every row landed untenanted and each scoped reader saw nothing.
        //
        // Tenant A gets 2 rows (1 completed), tenant B gets 1, so a leak returns a non-zero and
        // distinguishable count rather than merely a wrong one.
        var (tenantAStore, _) = Create(TenantA);
        var (tenantBStore, _) = Create(TenantB);

        await tenantAStore.SaveAsync(NewSaga(completed: false, tenantId: TenantA), ct).ConfigureAwait(false);
        await tenantAStore.SaveAsync(NewSaga(completed: true, tenantId: TenantA), ct).ConfigureAwait(false);
        await tenantBStore.SaveAsync(NewSaga(completed: false, tenantId: TenantB), ct).ConfigureAwait(false);
    }

    [Fact]
    public async Task AScopedCallerSupplyingAnotherTenantsFilterGetsTheEmptyIntersection()
    {
        await InitAsync().ConfigureAwait(false);
        var ct = CancellationToken.None;
        try
        {
            await SeedTwoTenantsAsync(ct).ConfigureAwait(false);

            // SAFETY — scoped to tenant A, the caller supplies tenant B's id in the filter. The ambient scope
            // is INTERSECTED with the filter, so the WHERE becomes tenant_id = 'tenant-b' AND tenant_id =
            // 'tenant-a' → no rows. A substitution (filter replaces ambient) would return tenant B's row.
            var (_, adminA) = Create(TenantA);

            var crossTenant = await adminA.QuerySagasAsync(
                new SagaQueryFilter { TenantId = TenantB }, ct).ConfigureAwait(false);

            crossTenant.Count.ShouldBe(
                0,
                "a caller scoped to tenant A supplying tenant B's filter id must get the EMPTY intersection, not "
                + "tenant B's rows — the ambient scope is intersected with (not substituted by) the caller filter.");
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task AScopedCallerStillSeesItsOwnTenantsRows()
    {
        await InitAsync().ConfigureAwait(false);
        var ct = CancellationToken.None;
        try
        {
            await SeedTwoTenantsAsync(ct).ConfigureAwait(false);

            // LIVENESS — the intersection must not make the store inert: a caller scoped to tenant A, with no
            // caller filter, still sees exactly its own 2 rows (and never tenant B's 1). This is the arm a
            // "returns nothing to anyone" implementation fails while still passing SAFETY.
            var (_, adminA) = Create(TenantA);

            var own = await adminA.QuerySagasAsync(new SagaQueryFilter(), ct).ConfigureAwait(false);

            own.Count.ShouldBe(2, "a caller scoped to tenant A still sees exactly its own 2 sagas.");
            own.ShouldAllBe(s => s.TenantId == TenantA, "the scoped read must return only tenant A's rows.");
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task StatisticsIntersectTheAmbientScopeWhileAnUnscopedOperatorStaysEstateWide()
    {
        await InitAsync().ConfigureAwait(false);
        var ct = CancellationToken.None;
        try
        {
            await SeedTwoTenantsAsync(ct).ConfigureAwait(false);

            // SAFETY — a scoped operator's statistics reflect only their own tenant (2 = tenant A's rows), not
            // the estate. RED if statistics dropped the ambient predicate.
            var (_, adminA) = Create(TenantA);
            var scopedStats = await adminA.GetStatisticsAsync(ct).ConfigureAwait(false);
            scopedStats.TotalCount.ShouldBe(2, "a caller scoped to tenant A counts only tenant A's 2 sagas.");
            scopedStats.CompletedCount.ShouldBe(1, "tenant A has exactly 1 completed saga.");

            // LIVENESS — an UNSCOPED operator still receives estate-wide statistics (all 3). Scoping this
            // away would break the legitimate operator diagnostic rather than secure it.
            //
            // Statistics and summaries diverge here ON PURPOSE, and the two requests sit in one file
            // saying opposite things: the summaries query makes the ambient term unconditional, because
            // dropping it when no scope was set handed a multi-tenant deployment every tenant's saga ids
            // and types; statistics keeps it conditional, because counts alone are the operator's
            // estate-wide diagnostic. Only the identifying data is closed off.
            //
            // Worth settling rather than inheriting: the interface already carries an explicit
            // PurgeAllTenantsCompletedBeforeAsync, so "estate-wide" is expressible as its own operation
            // instead of being inferred from an unset scope — which is the shape that fails open when a
            // host forgets to establish one. That is a contract decision, not a test fix.
            var (_, adminAll) = Create(tenantId: null);
            var estateStats = await adminAll.GetStatisticsAsync(ct).ConfigureAwait(false);
            estateStats.TotalCount.ShouldBe(3, "an unscoped operator still sees the estate-wide total (3 sagas).");
            estateStats.CompletedCount.ShouldBe(1, "and the estate's single completed saga.");
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>SQL Server saga-admin tenant-intersection lock — never skipped.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreAdminTenantIntersectionShould
    : SagaStoreAdminTenantIntersectionTestBase, IClassFixture<SqlServerSagaStoreContainerFixture>
{
    private readonly SqlServerSagaStoreContainerFixture _fixture;

    public SqlServerSagaStoreAdminTenantIntersectionShould(SqlServerSagaStoreContainerFixture fixture) => _fixture = fixture;

    /// <inheritdoc/>
    protected override async Task InitAsync()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "SQL Server container must be available — this cross-tenant intersection lock is never skipped.");
        await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
        await _fixture.CleanupTableAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override Task CleanupAsync() => _fixture.CleanupTableAsync();

    /// <inheritdoc/>
    protected override (ISagaStore Store, ISagaStoreAdmin Admin) Create(string? tenantId)
    {
        var store = new SqlServerSagaStore(
            _fixture.ConnectionString,
            NullLogger<SqlServerSagaStore>.Instance,
            new DispatchJsonSerializer(),
            tenantId is null ? null : new FixedTenant(tenantId));
        return (store, (ISagaStoreAdmin)store);
    }
}

/// <summary>PostgreSQL saga-admin tenant-intersection lock — never skipped.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Postgres")]
[Collection("PostgresSagaStore")]
public sealed class PostgresSagaStoreAdminTenantIntersectionShould : SagaStoreAdminTenantIntersectionTestBase
{
    private readonly PostgresSagaStoreContainerFixture _fixture;

    public PostgresSagaStoreAdminTenantIntersectionShould(PostgresSagaStoreContainerFixture fixture) => _fixture = fixture;

    /// <inheritdoc/>
    protected override async Task InitAsync()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "PostgreSQL container must be available — this cross-tenant intersection lock is never skipped.");
        await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
        await _fixture.CleanupTableAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override Task CleanupAsync() => _fixture.CleanupTableAsync();

    /// <inheritdoc/>
    protected override (ISagaStore Store, ISagaStoreAdmin Admin) Create(string? tenantId)
    {
        var options = Options.Create(new PostgresSagaOptions
        {
            ConnectionString = _fixture.ConnectionString,
            Schema = _fixture.Schema,
            TableName = _fixture.TableName,
            CommandTimeoutSeconds = 30,
        });

        var store = new PostgresSagaStore(
            options,
            NullLogger<PostgresSagaStore>.Instance,
            new DispatchJsonSerializer(),
            tenantId is null ? null : new FixedTenant(tenantId));
        return (store, (ISagaStoreAdmin)store);
    }
}
