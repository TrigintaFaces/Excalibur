// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.Data.DeadLetter;

/// <summary>
/// Real-SqlServer lock on the reach of the age-based retention purge,
/// <see cref="SqlServerDeadLetterQueue.PurgeAllTenantsEntriesOlderThanAsync"/>, which is estate-wide by design and
/// stays estate-wide whether or not a tenant is ambient.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this locks, and why it is not the obvious thing.</b> The retention purge is the one destructive
/// write on this surface that selects by AGE alone rather than by entry id, which makes it look like the
/// place a tenant term belongs. It is not. This operation is reached through
/// <see cref="IDeadLetterQueueAdmin"/> - the privileged operator surface - and estate-wide reach is the
/// capability it exists to provide. Confining the DELETE to the ambient tenant does not narrow that
/// capability, it <em>deletes</em> it: a tenant context resolves in every multi-tenant host, so a confined
/// purge leaves such an operator no way to run estate retention at all, and both outcomes come back as an
/// indistinguishable <see cref="int"/>. The reach is stated in the method name for that reason, matching the
/// convention this repository already ships across its providers (<c>CleanupAllTenantsSentMessagesAsync</c>).
/// </para>
/// <para>
/// <b>This lock RED-detects a change we nearly shipped.</b> A candidate fix appended
/// <c>AND TenantId = @ScopeTenantId</c> to this DELETE. Every arm below except the no-context one goes RED
/// against that build, which is the whole point of the class: an arm that has only ever been green is not a
/// lock. The discriminating assertion is that tenant B's expired entry is destroyed by a purge invoked while
/// tenant A is ambient - the exact behaviour a tenant term removes.
/// </para>
/// <para>
/// <b>Real infrastructure, never skipped.</b> What a DELETE predicate matches is decided by the database
/// engine, not by the caller. A mocked connection returns the row count it was handed and would certify a
/// tenant-confined purge as estate-wide just as readily as it would certify the reverse, which is how a
/// change to this predicate survives a unit suite in either direction.
/// </para>
/// <para>
/// <b>Both halves are mandatory (testing-patterns §3).</b> The LIVENESS half - every tenant's expired entries
/// are actually removed - is what a narrowed or inert purge fails; a purge that deletes nothing satisfies any
/// assertion phrased as "X survived". The SAFETY half is that the age term still bounds the delete, so
/// entries inside the retention window survive for every tenant. Estate reach without the age bound would
/// empty the queue.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
public sealed class SqlServerDeadLetterQueuePurgeTenantScopeShould(SqlServerContainerFixture fixture)
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	/// <summary>The retention window under test: entries older than this are eligible for purge.</summary>
	private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(7);

	private readonly SqlServerContainerFixture _fixture = fixture;

	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	[Fact]
	public async Task PurgeEveryTenantsExpiredEntries_EvenWhenATenantIsAmbient()
	{
		// KEYSTONE - LIVENESS. The arm that RED-detects the rejected tenant-confining fix, and the arm that
		// proves the operator capability still exists. An ambient tenant must not narrow the reach of this
		// operation: a platform operator running estate retention from inside a tenant-resolved host is the
		// ordinary case, not an edge case, because ITenantContext resolves whenever multi-tenancy is
		// registered at all.
		//
		// Note what this arm rules out that a "tenant A's own entry was purged" assertion does not: a purge
		// confined to tenant A also removes A's entry, and would pass that weaker assertion while having
		// silently lost the estate. Only naming B's entry discriminates.
		await ResetAsync().ConfigureAwait(false);

		var aOld = await SeedAsync(TenantA, Expired).ConfigureAwait(false);
		var bOld = await SeedAsync(TenantB, Expired).ConfigureAwait(false);

		// Fixture liveness: the seed must actually have written both rows, or every "was deleted" assertion
		// below is vacuously satisfied by a table that was empty to begin with.
		var seeded = await SurvivingIdsAsync().ConfigureAwait(false);
		seeded.ShouldBe([aOld, bOld], ignoreOrder: true,
			"the seed must have written both tenants' entries - an empty table satisfies every 'was purged' " +
			"assertion below while proving nothing");

		var purged = await QueueFor(TenantA).PurgeAllTenantsEntriesOlderThanAsync(RetentionWindow, CancellationToken.None)
			.ConfigureAwait(false);

		var surviving = await SurvivingIdsAsync().ConfigureAwait(false);

		surviving.ShouldNotContain(bOld,
			"the retention purge is estate-wide and must remain so while a tenant is ambient. A DELETE " +
			"carrying a tenant term (WHERE EnqueuedAt < @Cutoff AND TenantId = @ScopeTenantId) leaves this " +
			"entry behind, which does not narrow the operator capability but removes it: every multi-tenant " +
			"host resolves a tenant context, so no caller would be able to run estate retention at all.");

		surviving.ShouldNotContain(aOld,
			"the ambient tenant's own expired entry is purged too - this operation is bounded by age, not " +
			"by tenant");

		surviving.ShouldBeEmpty("both seeded entries were older than the cutoff, so nothing survives");

		// The returned count is part of the contract, not decoration. It is also the only signal a caller
		// gets: a confined purge and an estate-wide one both return an int, so a count of 1 here is the lost
		// capability reported indistinguishably from success.
		purged.ShouldBe(2,
			"the purge must report exactly the rows it deleted - both tenants' expired entries. A count of 1 " +
			"means the DELETE was confined to the ambient tenant and the estate-wide capability is gone.");
	}

	[Fact]
	public async Task PreserveEntriesNewerThanTheCutoff_ForEveryTenant()
	{
		// SAFETY - the age term still bounds the delete. Estate reach and an age bound are independent
		// properties and this arm holds them apart: a statement that reaches every tenant but has lost its
		// cutoff would empty the queue of live, still-actionable dead letters, and a statement scoped to the
		// tenant is the rejected design above. Only the conjunction survives this arm together with the
		// keystone.
		//
		// This arm is also RED against the rejected fix (bOld survives it), which is deliberate: the two
		// arms fail for different reasons and separate the diagnosis. If the keystone alone fails, the reach
		// changed; if this one alone fails, the age term did.
		await ResetAsync().ConfigureAwait(false);

		var aOld = await SeedAsync(TenantA, Expired).ConfigureAwait(false);
		var bOld = await SeedAsync(TenantB, Expired).ConfigureAwait(false);
		var aNew = await SeedAsync(TenantA, WithinRetention).ConfigureAwait(false);
		var bNew = await SeedAsync(TenantB, WithinRetention).ConfigureAwait(false);

		var purged = await QueueFor(TenantA).PurgeAllTenantsEntriesOlderThanAsync(RetentionWindow, CancellationToken.None)
			.ConfigureAwait(false);

		var surviving = await SurvivingIdsAsync().ConfigureAwait(false);

		surviving.ShouldBe([aNew, bNew], ignoreOrder: true,
			"exactly the entries older than the cutoff are removed, in every tenant. Entries inside the " +
			"retention window survive regardless of which tenant owns them and regardless of which tenant " +
			"is ambient.");

		surviving.ShouldNotContain(aOld, "the ambient tenant's expired entry is eligible on the age term");
		surviving.ShouldNotContain(bOld,
			"the other tenant's expired entry is equally eligible - the selection is age, not ownership");

		purged.ShouldBe(2, "two rows satisfied the age term, one in each tenant");
	}

	[Fact]
	public async Task PurgeEstateWide_WhenNoTenantContextIsRegistered()
	{
		// NO-CONTEXT PATH. The single-tenant or host-operated deployment, where no ITenantContext is
		// registered at all. The behaviour here is identical to the keystone above, and that identity is the
		// contract being locked: the reach of this operation does not vary with whether a tenant context
		// happens to be present. Holding both arms is what makes "unambiguously estate-wide" falsifiable
		// rather than merely asserted - a build whose reach depended on the ambient context would pass one
		// of these two and fail the other.
		await ResetAsync().ConfigureAwait(false);

		var aOld = await SeedAsync(TenantA, Expired).ConfigureAwait(false);
		var bOld = await SeedAsync(TenantB, Expired).ConfigureAwait(false);
		var aNew = await SeedAsync(TenantA, WithinRetention).ConfigureAwait(false);

		var purged = await QueueFor(null).PurgeAllTenantsEntriesOlderThanAsync(RetentionWindow, CancellationToken.None)
			.ConfigureAwait(false);

		var surviving = await SurvivingIdsAsync().ConfigureAwait(false);

		surviving.ShouldBe([aNew], ignoreOrder: true,
			"with no tenant context registered the retention purge sweeps the estate. Both expired entries " +
			"go; the entry inside the retention window stays.");
		surviving.ShouldNotContain(aOld);
		surviving.ShouldNotContain(bOld);

		purged.ShouldBe(2, "both expired entries were removed");
	}

	/// <summary>An enqueue timestamp comfortably outside the retention window.</summary>
	private static DateTimeOffset Expired => DateTimeOffset.UtcNow - TimeSpan.FromDays(30);

	/// <summary>An enqueue timestamp comfortably inside the retention window.</summary>
	private static DateTimeOffset WithinRetention => DateTimeOffset.UtcNow - TimeSpan.FromHours(1);

	/// <param name="tenantId">
	/// The caller's tenant, or <see langword="null"/> for a host with no tenant of its own — modelled as the
	/// reserved untenanted partition, which is the term an absent context used to resolve to. A queue with no
	/// tenant context at all is no longer constructible.
	/// </param>
	/// <returns>A queue for that caller.</returns>
	/// <remarks>
	/// The arms below are unaffected by which identity this resolves: the retention purge carries no tenant
	/// term at all, which is the property they exist to lock, and the keystone already runs it with a real
	/// tenant ambient. The no-tenant arm still contrasts a caller with no tenant of its own against one that
	/// has one, and still fails if the DELETE ever acquires a tenant term.
	/// </remarks>
	private SqlServerDeadLetterQueue QueueFor(string? tenantId) =>
		new(
			() => new SqlConnection(_fixture.ConnectionString),
			new SqlServerDeadLetterQueueOptions(),
			NullLogger<SqlServerDeadLetterQueue>.Instance,
			tenantId is null
				? UntenantedTestTenantContext.Instance
				: new FixedTenantContext(tenantId),
			replayHandler: null);

	/// <summary>
	/// Writes a dead letter through the REAL enqueue path under <paramref name="tenantId"/> - so the stored
	/// tenant term is exactly what production stamps, not a value this test chose - then backdates only its
	/// <c>EnqueuedAt</c>, which the enqueue path hard-codes to now and offers no seam to control.
	/// </summary>
	private async Task<Guid> SeedAsync(string? tenantId, DateTimeOffset enqueuedAt)
	{
		var id = await QueueFor(tenantId).EnqueueAsync(
			new OrderPayload(Guid.NewGuid().ToString("N")),
			DeadLetterReason.MaxRetriesExceeded,
			CancellationToken.None).ConfigureAwait(false);

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		var updated = await connection.ExecuteAsync(
			"UPDATE [dbo].[DeadLetterQueue] SET EnqueuedAt = @EnqueuedAt WHERE Id = @Id",
			new { EnqueuedAt = enqueuedAt, Id = id }).ConfigureAwait(false);

		updated.ShouldBe(1, "the seeded entry must exist and have been backdated exactly once");

		return id;
	}

	private async Task<IReadOnlyList<Guid>> SurvivingIdsAsync()
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		var ids = await connection.QueryAsync<Guid>("SELECT Id FROM [dbo].[DeadLetterQueue]").ConfigureAwait(false);
		return [.. ids];
	}

	private async Task ResetAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the reach of an irreversible, age-selected DELETE is decided by the database engine - this " +
			"real-SqlServer lock must never be skipped; a skipped lock is the gap that ships a silent " +
			"change of reach in either direction");

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		// Mirrors Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql - TenantId is NOT NULL with
		// no default and is part of the primary key. Kept in lockstep with the shipped DDL (F-5: fixture DDL
		// is a sibling artifact of a schema change).
		_ = await connection.ExecuteAsync("""
			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterQueue]') AND type = N'U')
			BEGIN
			    CREATE TABLE [dbo].[DeadLetterQueue] (
			        Id                     UNIQUEIDENTIFIER NOT NULL,
			        TenantId               NVARCHAR(255)   NOT NULL,
			        MessageType            NVARCHAR(500)   NOT NULL,
			        Payload                VARBINARY(MAX)  NOT NULL,
			        Reason                 INT             NOT NULL,
			        ExceptionMessage       NVARCHAR(MAX)   NULL,
			        ExceptionStackTrace    NVARCHAR(MAX)   NULL,
			        EnqueuedAt             DATETIMEOFFSET  NOT NULL DEFAULT SYSDATETIMEOFFSET(),
			        OriginalAttempts       INT             NOT NULL DEFAULT 0,
			        Metadata               NVARCHAR(MAX)   NULL,
			        CorrelationId          NVARCHAR(255)   NULL,
			        CausationId            NVARCHAR(255)   NULL,
			        SourceQueue            NVARCHAR(255)   NULL,
			        IsReplayed             BIT             NOT NULL DEFAULT 0,
			        ReplayedAt             DATETIMEOFFSET  NULL,
			        CONSTRAINT PK_DeadLetterQueue PRIMARY KEY (Id, TenantId)
			    );
			END
			""").ConfigureAwait(false);

		_ = await connection.ExecuteAsync("DELETE FROM [dbo].[DeadLetterQueue]").ConfigureAwait(false);
	}

	private sealed record OrderPayload(string OrderId);
}
