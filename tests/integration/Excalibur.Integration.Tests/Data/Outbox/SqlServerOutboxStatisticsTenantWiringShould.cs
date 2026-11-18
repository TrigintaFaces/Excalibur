// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CA2100 // SQL strings use compile-time fixture constants for schema/table names.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// NON-SKIPPED real-SQL-Server lock on estate-wide outbox statistics, resolved through the
/// production registration seam rather than constructed by hand.
/// </summary>
/// <remarks>
/// The historical registration defect made a backlog report appear empty when tenanted rows were
/// present. The current contract is explicitly estate-wide: writes carry their own tenant and statistics
/// counts the whole table, independent of the ambient tenant. This fixture keeps a changing tenant
/// context to prove that the operator report still includes both persisted tenants through real DI.
/// Its deployment mode must therefore explicitly require tenants, matching that resolving context.
/// An independent SQL count proves both writes landed and that the report neither hides nor invents rows.
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
	/// LIVENESS and SAFETY as a pair: both tenants' persisted messages are counted, and no rows
	/// beyond those actually present are invented.
	/// </summary>
	/// <remarks>
	/// Both messages are staged through the same store and table with different tenants. The selected
	/// ambient tenant must not confine this estate-wide report. The independent row count distinguishes
	/// a real measurement from an empty or fabricated result.
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
	/// The resolving context is registered before the provider's single-tenant default, together with
	/// required-tenant deployment mode so startup validation sees a consistent multi-tenant composition.
	/// The outbox persists each message's explicit tenant; its estate-wide statistics deliberately reads
	/// no ambient tenant. The store is still resolved through the actual provider registration.
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
		_ = services.Configure<TenantContextOptions>(static options => options.RequireTenant = true);

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
