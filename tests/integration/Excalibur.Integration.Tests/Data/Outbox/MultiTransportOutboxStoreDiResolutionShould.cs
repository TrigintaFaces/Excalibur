// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MultiTransport;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-engine DI-resolution lock (hwe9c3) for the multi-transport routing decorator
/// <see cref="MultiTransportOutboxStore"/>. Every existing outbox suite hand-constructs its store directly
/// (<c>SqlServerOutboxKeystoneRoundTripShould.CreateStore()</c> and siblings), which proves the store works
/// when handed the right options - never that <c>AddMultiTransportOutbox</c> actually wires it onto the
/// registered <c>IOutboxStore</c>. This suite resolves <see cref="IMultiTransportOutboxRouter"/> through the
/// real container built by <c>AddOutbox(o => o.UseSqlServer(...))</c> + <c>AddMultiTransportOutbox(...)</c>,
/// and proves the <c>TargetTransports</c> CSV the bead calls out survives the real SQL Server INSERT/reload.
/// </summary>
/// <remarks>
/// <para>
/// WIRE proof: the resolved <see cref="IMultiTransportOutboxRouter"/> is the routing decorator, and it
/// wraps the REAL keyed "default" <c>IOutboxStore</c> the DI container registered - not a store the test
/// constructed by hand.
/// </para>
/// <para>
/// BEHAVIOUR proof: a message routed to multiple transports carries the resolved, comma-joined
/// <c>TargetTransports</c> through a real SQL Server INSERT and reload - a dropped options dependency, or a
/// wiring bug that left the router pointed at the wrong inner store, would surface here.
/// </para>
/// <para>Never skipped: an absent Docker daemon fails the arm rather than passing silently.</para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Outbox")]
public sealed class MultiTransportOutboxStoreDiResolutionShould
{
	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public MultiTransportOutboxStoreDiResolutionShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ResolveIMultiTransportOutboxRouter_WrappingTheRealSqlServerStore_ThroughAddMultiTransportOutbox()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		using var provider = BuildWiredStack();

		var router = provider.GetRequiredService<IMultiTransportOutboxRouter>();
		_ = router.ShouldBeOfType<MultiTransportOutboxStore>(
			"hwe9c3: AddMultiTransportOutbox() must resolve IMultiTransportOutboxRouter to the routing "
			+ "decorator, built over the REAL registered store - a dropped options dependency would fail "
			+ "construction, and a wiring bug could leave it wrapping the wrong store.");
	}

	[Fact]
	public async Task RouteAndPersistTargetTransportsCsv_ThroughARealSqlServerInsertAndReload()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		using var provider = BuildWiredStack();

		var router = provider.GetRequiredService<IMultiTransportOutboxRouter>();
		var messageId = Guid.NewGuid().ToString();
		var message = new OutboundMessage("Di.MultiTransport.MessageType", "payload"u8.ToArray(), "destination")
		{
			Id = messageId,
		};

		await router.PublishToTransportsAsync(
			["kafka", "rabbitmq"], message, CancellationToken.None).ConfigureAwait(false);

		// Read back through the REAL keyed "default" store the router wraps - the CSV the router computed
		// must have survived the real SQL Server INSERT/reload, not merely been set in memory.
		var store = provider.GetRequiredKeyedService<Excalibur.Dispatch.IOutboxStore>("default");
		var reloaded = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
			.Single(m => m.Id == messageId);

		reloaded.TargetTransports.ShouldBe("kafka,rabbitmq");
		reloaded.IsMultiTransport.ShouldBeTrue();
	}

	private async Task EnsureReadyAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"[infrastructure-unavailable] SQL Server (Docker) is not available - this real-engine "
			+ "multi-transport DI-resolution lock is never satisfied by not running.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private ServiceProvider BuildWiredStack()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddExcalibur(x => x.AddOutbox(o => o.UseSqlServer(sql =>
			_ = sql.ConnectionString(_fixture.ConnectionString))));

		_ = services.AddMultiTransportOutbox(o =>
		{
			o.DefaultTransport = "kafka";
			o.RequireExplicitBindings = false;
		});

		return services.BuildServiceProvider();
	}
}
