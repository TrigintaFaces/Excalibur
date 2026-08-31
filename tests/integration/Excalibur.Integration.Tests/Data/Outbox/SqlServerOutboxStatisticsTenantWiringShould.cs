// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CA2100 // SQL strings use compile-time fixture constants for schema/table names.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// NON-SKIPPED real-SQL-Server lock on the outbox <b>statistics</b> tenant term, resolved through the
/// production registration seam rather than constructed by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this resolves through DI instead of calling the constructor.</b> The statistics statement is the
/// one relational outbox statement that takes no message identifier, so its tenant predicate is the only
/// thing confining it, and the store resolves that predicate from the ambient tenant context it was built
/// with. Every construction site in the provider's registration omitted that context, so the store was
/// built with none on every path — including a multi-tenant host. A test that constructs the store itself
/// chooses what the registration failed to supply, and therefore cannot see the defect. This lock builds
/// the store the way a host does and asserts what that host observes.
/// </para>
/// <para>
/// <b>What the operator sees when it is wrong.</b> The absent case of the keyed partition is the reserved
/// untenanted sentinel, so a store built without a context emits
/// <c>COALESCE(TenantId, sentinel) = sentinel</c> while the write path persists each message's real
/// tenant. The read operand is a constant and the write operand is the actual tenant, so they never match
/// and every tenanted message is aggregated by nothing. A backlog alert built on this reads empty while
/// the outbox fills, which is worse than an error because nothing draws attention to it.
/// </para>
/// <para>
/// <b>Both arms are required.</b> A statistics call that counts nothing at all satisfies the confinement
/// property perfectly, so the safety arm alone cannot distinguish a correctly scoped report from an inert
/// one. The liveness arm is asserted alongside it, and a direct row count over a separate connection
/// proves both messages are on disk — so a zero is attributable to the predicate rather than to a write
/// that never landed.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Core")]
public sealed class SqlServerOutboxStatisticsTenantWiringShould
	: IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const string OwningTenant = "tenant-stats-wiring-a";
	private const string OtherTenant = "tenant-stats-wiring-b";

	private readonly SqlServerOutboxStoreContainerFixture _fixture;
	private readonly MutableTenantContext _tenantContext = new();

	public SqlServerOutboxStatisticsTenantWiringShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// LIVENESS and SAFETY as a pair: the owning tenant's staged message is counted, and the other
	/// tenant's staged message is not.
	/// </summary>
	/// <remarks>
	/// Both messages are staged through the same store, in the same table, differing only in their tenant,
	/// so nothing but the tenant term can explain a difference in the counts. Asserting the pair is what
	/// separates a scoped report from a report that returns zero to everybody.
	/// </remarks>
	[Fact]
	public async Task Count_every_tenants_staged_message_because_the_report_is_estate_wide()
	{
		var store = await BuildStoreThroughRegistrationAsync().ConfigureAwait(false);

		_tenantContext.TenantId = OwningTenant;
		await StageAsync(store, "outbox-stats-wiring-a", OwningTenant).ConfigureAwait(false);

		_tenantContext.TenantId = OtherTenant;
		await StageAsync(store, "outbox-stats-wiring-b", OtherTenant).ConfigureAwait(false);

		// Control: both rows are in the table, read directly. Without this, a zero below is
		// indistinguishable from an empty table or a staging path that silently did nothing.
		(await CountRowsAsync().ConfigureAwait(false)).ShouldBe(
			2,
			"both messages must be persisted before statistics is asked anything, otherwise the counts "
			+ "below prove nothing about the predicate");

		_tenantContext.TenantId = OwningTenant;
		var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// LIVENESS — every staged message is reported, whichever tenant staged it. This operation is
		// deliberately estate-wide: it takes no tenant argument, the statistics type it returns carries no
		// tenant field, and this store reads no ambient tenant context, so a confined result has no way to
		// say which partition it describes. An implementation that confined it would report zero to a host
		// with no ambient tenant and hide a real backlog — the failure this arm exists to catch.
		stats.StagedMessageCount.ShouldBe(
			2,
			"outbox statistics is an operator report over the whole table. Reporting fewer than the rows "
			+ "present is the defect: a dashboard that shows an empty outbox while messages accumulate "
			+ "hides exactly the backlog it exists to reveal");

		// SAFETY — the count is the rows that exist, not more. Asserted through the same value so an
		// implementation cannot satisfy the arm above by inventing rows, and so a broken store returning a
		// constant fails both arms rather than passing one.
		stats.StagedMessageCount.ShouldBe(
			await CountRowsAsync().ConfigureAwait(false),
			"the reported count must equal the rows actually in the table, so the report is a measurement "
			+ "rather than an assumption");
	}

	/// <summary>
	/// Registers the outbox store the way a host does, then resolves it — the seam this lock binds.
	/// </summary>
	/// <remarks>
	/// The ambient context is registered BEFORE the provider registration so the provider's fail-closed
	/// single-tenant default (a <c>TryAdd</c>) yields to it, matching the composition order a multi-tenant
	/// host produces. Nothing here hands the store a tenant context directly: if the registration does not
	/// thread the resolved context into construction, the resolved store has none.
	/// </remarks>
	private async Task<SqlServerOutboxStore> BuildStoreThroughRegistrationAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — an operator backlog alert reading empty while the "
			+ "outbox fills is a silent failure, so this real-infra lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		services.AddSingleton<ITenantContext>(_tenantContext);

		var connectionString = _fixture.ConnectionString;
		var schemaName = _fixture.SchemaName;
		var outboxTableName = _fixture.OutboxTableName;
		var transportsTableName = _fixture.TransportsTableName;

		_ = services.AddSqlServerOutboxStore(options =>
		{
			options.ConnectionString = connectionString;
			options.ProcessorId = "proc-stats-wiring";
			options.Tables.SchemaName = schemaName;
			options.Tables.OutboxTableName = outboxTableName;
			options.Tables.TransportsTableName = transportsTableName;
			options.Processing.CommandTimeoutSeconds = 30;
		});

		var provider = services.BuildServiceProvider();

		// The keyed IOutboxStore alias forwards to this same singleton, so resolving the concrete type
		// observes exactly the instance a consumer receives. Statistics is declared on the admin seam
		// (IOutboxStoreAdmin), which this provider surfaces on the store itself rather than as a
		// separately-registered service, so the concrete type is the resolvable form of that seam.
		return provider.GetRequiredService<SqlServerOutboxStore>();
	}

	private static Task StageAsync(SqlServerOutboxStore store, string id, string tenantId) =>
		store.StageMessageAsync(
			new OutboundMessage("Stats.Wiring", [1], "dest") { Id = id, TenantId = tenantId },
			CancellationToken.None).AsTask();

	/// <summary>
	/// Counts rows over an independent connection, so the control cannot inherit the store's own tenant
	/// predicate — the very thing under test.
	/// </summary>
	private async Task<int> CountRowsAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var sql = $"SELECT COUNT(*) FROM [{_fixture.SchemaName}].[{_fixture.OutboxTableName}]";
		await using var command = new SqlCommand(sql, connection);
		return (int)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
	}

	/// <summary>An ambient context whose tenant the test moves between calls, as a resolver would.</summary>
	private sealed class MutableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
