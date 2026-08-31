// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Independent (author≠impl, TestsDeveloper) NON-SKIPPED real-SQL-Server lock for the split between the
/// tenant-confined consumer read (<see cref="IMultiTransportOutboxStore.GetTransportDeliveriesAsync"/>) and
/// the estate-wide delivery-drain read (<see cref="IMultiTransportOutboxStoreAdmin.GetAllTenantsTransportDeliveriesAsync"/>).
/// </summary>
/// <remarks>
/// <para>
/// Before this fix, the interface exposed only one operation, reachable with any message id regardless of
/// which tenant's rows the caller could see — a caller scoped to tenant A supplying tenant B's message id
/// received B's delivery records. The fix binds the tenant as an explicit SQL predicate, evaluated by the
/// server, and moves the drain's unconfined read to a separate, named admin operation. This lock exercises
/// all three arms real SQL Server, with two tenants and one message id, per the bead's own acceptance
/// criterion.
/// </para>
/// <para>
/// <b>Non-vacuity, verified by mutation</b> (the pre-fix interface had no <c>tenantId</c> parameter at all, so
/// a test bound to the new signature cannot compile against the old one): removing the
/// <c>AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId</c> predicate from
/// <c>GetTenantScopedTransportDeliveriesRequest</c>'s SQL and re-running against real SQL Server turned
/// <see cref="TenantAScopedReadOfTenantBsMessage_ReturnsEmpty"/> RED — 1 failed / 3 passed, the unscoped SQL
/// returning tenant B's row instead of nothing, exactly the disclosure this bead reports. The predicate was
/// then restored (confirmed byte-identical) and the suite re-run GREEN, 4/4.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Core")]
public sealed class SqlServerTransportDeliveriesTenantScopeShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerTransportDeliveriesTenantScopeShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY. A caller scoped to tenant A that supplies tenant B's message id receives nothing — the
	/// confinement the deleted <c>TransportDeliveryReadTenantTermShould</c> documented as fixed.
	/// </summary>
	[Fact]
	public async Task TenantAScopedReadOfTenantBsMessage_ReturnsEmpty()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "transport-scope-cross-tenant";

		await StageMultiTransportMessageAsync(store, messageId, TenantB, "kafka").ConfigureAwait(false);

		var deliveries = (await store.GetTransportDeliveriesAsync(messageId, TenantA, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		deliveries.ShouldBeEmpty(
			"a caller scoped to tenant A must not receive tenant B's delivery records, even when it supplies " +
			"tenant B's own message id");
	}

	/// <summary>
	/// LIVENESS. A caller scoped to tenant A that supplies its OWN message id still receives its rows — proves
	/// the scoped read is not merely inert (a store that returns nothing to anybody would pass the safety arm
	/// above vacuously).
	/// </summary>
	[Fact]
	public async Task TenantAScopedReadOfTenantAsOwnMessage_ReturnsDeliveries()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "transport-scope-own-tenant";

		await StageMultiTransportMessageAsync(store, messageId, TenantA, "kafka").ConfigureAwait(false);

		var deliveries = (await store.GetTransportDeliveriesAsync(messageId, TenantA, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		deliveries.ShouldHaveSingleItem();
		deliveries[0].TransportName.ShouldBe("kafka");
	}

	/// <summary>
	/// DRAIN ARM. The estate-wide admin operation returns rows for a tenanted message with no tenant argument
	/// at all — proving the split did not just re-break the delivery drain the original bug (<c>76ae75e93</c>
	/// -class regression) fixed. Exercised for both tenants to show it is not confined to either.
	/// </summary>
	[Theory]
	[InlineData(TenantA)]
	[InlineData(TenantB)]
	public async Task EstateWideAdminRead_ReturnsDeliveries_ForEveryTenant(string owningTenant)
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var admin = (IMultiTransportOutboxStoreAdmin)store;
		var messageId = $"transport-scope-drain-{owningTenant}";

		await StageMultiTransportMessageAsync(store, messageId, owningTenant, "rabbitmq").ConfigureAwait(false);

		var deliveries = (await admin.GetAllTenantsTransportDeliveriesAsync(messageId, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		deliveries.ShouldHaveSingleItem(
			$"the estate-wide drain read must return {owningTenant}'s delivery with no tenant argument supplied");
		deliveries[0].TransportName.ShouldBe("rabbitmq");
	}

	private static async Task StageMultiTransportMessageAsync(
		IMultiTransportOutboxStore store, string messageId, string tenantId, string transportName)
	{
		var message = new OutboundMessage
		{
			Id = messageId,
			MessageType = "T",
			Payload = [1],
			Destination = "dest",
			TenantId = tenantId,
			IsMultiTransport = true,
		};
		var transports = new[] { new OutboundMessageTransport(messageId, transportName) { Destination = "d" } };

		await store.StageMessageWithTransportsAsync(message, transports, CancellationToken.None).ConfigureAwait(false);
	}

	private async Task<SqlServerOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — real-infra transport-delivery tenant-scope lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var options = new SqlServerOutboxOptions { ConnectionString = _fixture.ConnectionString };
		return new SqlServerOutboxStore(
			() => new SqlConnection(_fixture.ConnectionString),
			options,
			payloadSerializer: null,
			NullLogger<SqlServerOutboxStore>.Instance);
	}
}
