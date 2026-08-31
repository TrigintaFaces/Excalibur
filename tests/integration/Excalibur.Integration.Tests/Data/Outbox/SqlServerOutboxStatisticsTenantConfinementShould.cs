// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA2100 // SQL strings use compile-time fixture constants for schema/table names.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Independent (author != impl) NON-SKIPPED real-SQL-Server lock on the outbox <b>statistics</b> tenant
/// term — the one relational outbox statement that is tenant-confined by design.
/// </summary>
/// <remarks>
/// <para>
/// The subsystem's architecture contract names statistics as the single confined statement, because it
/// takes no message identifier and aggregates whatever its predicate admits, so the tenant term is the
/// only thing standing between a per-tenant report and an estate-wide one. It constrains on
/// <c>COALESCE(TenantId, @UntenantedSentinel) = @TenantId</c>.
/// </para>
/// <para>
/// <b>Why the existing conformance suite cannot see this.</b> Every statistics arm in the shared outbox
/// conformance base stages messages built by its <c>CreateTestMessage</c> helper, which sets no
/// <c>TenantId</c>. Those rows land in the reserved untenanted partition — the very partition the
/// predicate matches when no ambient tenant is registered. The arms therefore exercise the one input
/// for which the statement cannot fail, and a store that reports zero for every tenanted message passes
/// all of them. The blindness is a property of the fixture data, not of the assertions.
/// </para>
/// <para>
/// <b>Pairing.</b> The tenanted case is the arm under test; the untenanted case is its positive control.
/// Without the control, a RED on the first arm is indistinguishable from a broken fixture, an empty
/// table, or a container that never came up. The control staging through the same store, the same
/// connection and the same statistics call is what makes the tenanted RED attributable to the tenant
/// term rather than to the harness.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Core")]
public sealed class SqlServerOutboxStatisticsTenantConfinementShould
	: IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const string OwningTenant = "tenant-statistics-a";

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxStatisticsTenantConfinementShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// POSITIVE CONTROL: an untenanted staged message is counted.
	/// </summary>
	/// <remarks>
	/// This is the single-tenant host's shape, and it is the input the shared conformance suite already
	/// covers. It must stay GREEN. If it ever goes RED the tenanted arm below carries no information,
	/// because a statistics call that counts nothing at all would satisfy it for the wrong reason.
	/// </remarks>
	[Fact]
	public async Task Count_an_untenanted_staged_message_so_the_tenanted_arm_is_attributable()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		await store.StageMessageAsync(
			new OutboundMessage("T", [1], "dest") { Id = "outbox-stats-untenanted" },
			CancellationToken.None).ConfigureAwait(false);

		var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.StagedMessageCount.ShouldBe(
			1,
			"the untenanted partition is the control: statistics must count a message staged without a "
			+ "tenant, otherwise the tenanted assertion below proves nothing");
	}

	/// <summary>
	/// The arm under test: a message staged under a real tenant must still be counted by that tenant's
	/// statistics report.
	/// </summary>
	/// <remarks>
	/// A store constructed without an <c>ITenantContext</c> — the default registration shape, and the one
	/// every existing conformance deriver uses — resolves the untenanted sentinel. A tenanted row then
	/// fails <c>COALESCE(TenantId, sentinel) = sentinel</c> and is aggregated by nothing. The operator
	/// facing that host sees an outbox reporting zero staged messages while messages accumulate in the
	/// table, so the backlog that statistics exists to reveal is the one thing it cannot show.
	/// </remarks>
	[Fact]
	public async Task Count_a_tenanted_staged_message_rather_than_reporting_an_empty_outbox()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		await store.StageMessageAsync(
			new OutboundMessage("T", [1], "dest")
			{
				Id = "outbox-stats-tenanted",
				TenantId = OwningTenant,
			},
			CancellationToken.None).ConfigureAwait(false);

		// The row is in the table — proven directly, so a RED below is the predicate and not the write.
		(await ReadTenantAsync("outbox-stats-tenanted").ConfigureAwait(false))
			.ShouldBe(OwningTenant, "the write path must stamp the message's own tenant");

		var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.StagedMessageCount.ShouldBe(
			1,
			"a message staged under a real tenant is still a staged message: statistics reporting zero "
			+ "while the row sits in the table hides the backlog it exists to report");
	}

	private async Task<SqlServerOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra statistics tenant lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var options = new SqlServerOutboxOptions { ConnectionString = _fixture.ConnectionString };
		return new SqlServerOutboxStore(
			() => new SqlConnection(_fixture.ConnectionString),
			options,
			payloadSerializer: null,
			NullLogger<SqlServerOutboxStore>.Instance);
	}

	private async Task<string?> ReadTenantAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		var sql = $"SELECT TenantId FROM [{_fixture.SchemaName}].[{_fixture.OutboxTableName}] WHERE Id = @Id";
		await using var command = new SqlCommand(sql, connection);
		_ = command.Parameters.AddWithValue("@Id", messageId);
		var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
		return result is null or DBNull ? null : (string)result;
	}
}
