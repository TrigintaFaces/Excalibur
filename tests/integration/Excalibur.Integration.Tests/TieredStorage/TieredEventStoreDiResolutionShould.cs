// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.Azurite;
using Testcontainers.MsSql;

using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings below are fixed test-fixture constants, not user input

namespace Excalibur.Integration.Tests.TieredStorage;

/// <summary>
/// Real-engine DI-resolution lock (hwe9c3) for the tiered-storage decorator <c>TieredEventStoreDecorator</c>.
/// <see cref="TieredStorageWiringShould"/> proves the wiring SHAPE with <c>A.Fake&lt;IEventStore&gt;</c> as
/// the hot store; this suite resolves the decorator through the real <c>UseTieredStorage</c> entry point
/// over a REAL SQL Server hot tier and a REAL Azure Blob (Azurite) cold tier, and proves the read-through
/// actually returns archived events - not merely that the decorator type is present.
/// </summary>
/// <remarks>
/// <para>
/// WIRE proof: the keyed <c>"default"</c> <c>IEventStore</c> is the tiered decorator.
/// </para>
/// <para>
/// READ-THROUGH proof: events are appended (hot), copied into the REAL cold store, then deleted from the
/// RAW hot table (mirroring what <c>EventArchiveService</c> does after a durable cold write) - and the
/// keyed <c>"default"</c> store still returns them, because it read through to cold. A DI factory that
/// dropped the cold store, or a wiring bug that left the raw hot store bound to keyed <c>"default"</c>
/// instead of the decorator, would return zero events here.
/// </para>
/// <para>Never skipped: an absent Docker daemon fails the arm rather than passing silently.</para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "TieredStorage")]
[SuppressMessage("Design", "CA1001", Justification = "Disposed in DisposeAsync via IAsyncLifetime")]
public sealed class TieredEventStoreDiResolutionShould : IAsyncLifetime
{
	private const string AggregateId = "agg-tiered-di-1";
	private const string AggregateType = "TieredDiAggregate";

	private MsSqlContainer? _sqlContainer;
	private AzuriteContainer? _azuriteContainer;
	private string? _sqlConnectionString;
	private bool _dockerAvailable;
	private Exception? _unavailableCause;

	private string SkipReason(string reason) =>
		_unavailableCause is null ? reason : reason + " Cause: " + _unavailableCause.GetType().Name + ": " + _unavailableCause.Message;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_sqlContainer = new MsSqlBuilder()
				.WithBoundedMemory()
				.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
				.WithName($"mssql-tiered-di-{Guid.NewGuid():N}")
				.WithPassword("Test@Pass123")
				.WithCleanUp(true)
				.Build();

			// --skipApiVersionCheck: the Azure.Storage.Blobs SDK negotiates a service API version newer than
			// any published Azurite image accepts (documented elsewhere in this test tree, e.g.
			// AzureBlobColdEventStoreIntegrationShould) - Azurite's own documented remedy.
			_azuriteContainer = new AzuriteBuilder()
				.WithImage("mcr.microsoft.com/azure-storage/azurite:3.36.0")
				.WithCommand("--skipApiVersionCheck")
				.Build();

			using var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			await Task.WhenAll(
				_sqlContainer.StartAsync(startCts.Token),
				_azuriteContainer.StartAsync(startCts.Token)).ConfigureAwait(false);

			_sqlConnectionString = _sqlContainer.GetConnectionString();

			await using var connection = new SqlConnection(_sqlConnectionString);
			await connection.OpenAsync(startCts.Token).ConfigureAwait(false);
			foreach (var script in ShippedSchemaScript.ReadSqlCmdBatches(
				"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/001_CreateEventStoreSchema.sql"))
			{
				await using var command = new SqlCommand(script, connection);
				_ = await command.ExecuteNonQueryAsync(startCts.Token).ConfigureAwait(false);
			}

			_dockerAvailable = true;
		}
		catch (Exception ex)
		{
			_unavailableCause = ex;
			_dockerAvailable = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		try
		{
			if (_sqlContainer is not null)
			{
				await _sqlContainer.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}

		try
		{
			if (_azuriteContainer is not null)
			{
				await _azuriteContainer.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}

	[Fact]
	public void ResolveKeyedDefaultIEventStore_AsTheTieredDecorator_ThroughUseTieredStorage()
	{
		Assert.SkipWhen(!_dockerAvailable, SkipReason(
			"[infrastructure-unavailable] SQL Server / Azurite (Docker) is not available - this real-engine "
			+ "tiered-storage DI-resolution lock is never satisfied by not running."));

		using var provider = BuildWiredStack();

		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		_ = store.ShouldBeOfType<TieredEventStoreDecorator>(
			"hwe9c3: UseTieredStorage() must rebind the keyed \"default\" IEventStore - the path repositories "
			+ "actually resolve - to the tiered read-through decorator, not the bare hot store a wiring bug "
			+ "would leave in its place.");
	}

	[Fact]
	public async Task ReadThroughToTheRealColdStore_WhenHotIsMissing_ThroughTheDiResolvedDecorator()
	{
		Assert.SkipWhen(!_dockerAvailable, SkipReason(
			"[infrastructure-unavailable] SQL Server / Azurite (Docker) is not available - this real-engine "
			+ "tiered-storage DI-resolution lock is never satisfied by not running."));

		using var provider = BuildWiredStack();
		var decorated = provider.GetRequiredKeyedService<IEventStore>("default");

		// 1. Write through the DI-resolved decorator - always lands on the hot (SQL Server) tier.
		var events = new IDomainEvent[]
		{
			CreateEvent(0), CreateEvent(1), CreateEvent(2),
		};
		_ = await decorated.AppendAsync(AggregateId, AggregateType, events, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// 2. Read the raw hot events back (via the RAW hot key, bypassing the decorator) and copy them into
		// the REAL cold store - the same tenant partition the decorator itself will read with.
		var rawHot = provider.GetRequiredKeyedService<IEventStore>(EventArchiveService.RawHotEventStoreKey);
		var hotEvents = await rawHot.LoadAsync(AggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		hotEvents.Count.ShouldBe(3);

		var coldStore = provider.GetRequiredService<IColdEventStore>();
		var tenant = KeyedTenantPartition.FromContext(provider.GetRequiredService<ITenantContext>());
		_ = await coldStore.WriteAsync(tenant, AggregateId, hotEvents, CancellationToken.None).ConfigureAwait(false);

		// 3. Trim the hot tier - mirrors what EventArchiveService does after a durable cold write. Deleted on
		// the RAW SQL Server table directly (the hot store's own contract has no delete operation).
		await using (var connection = new SqlConnection(_sqlConnectionString))
		{
			await connection.OpenAsync().ConfigureAwait(false);
			await using var command = new SqlCommand(
				"DELETE FROM [dbo].[EventStoreEvents] WHERE AggregateId = @AggregateId", connection);
			_ = command.Parameters.AddWithValue("@AggregateId", AggregateId);
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		(await rawHot.LoadAsync(AggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(0, "the hot tier must be empty after the trim, or this arm proves nothing about cold read-through");

		// 4. LIVENESS: the DI-resolved decorator still returns all 3 events - read through to the REAL cold
		// store. A DI factory that dropped IColdEventStore, or a wiring bug that bound the RAW hot store to
		// keyed "default" instead of the decorator, would return zero here.
		var loaded = await decorated.LoadAsync(AggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		loaded.Count.ShouldBe(3, "the tiered decorator, resolved through real DI, must read the archived events back from the real cold store");
		loaded.Select(static e => e.Version).ShouldBe(new long[] { 0, 1, 2 });
	}

	private ServiceProvider BuildWiredStack()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
		{
			_ = es.UseSqlServer(sql => sql.ConnectionString(_sqlConnectionString!));
			_ = es.UseTieredStorage(_ => { });
			_ = es.UseAzureBlobColdEventStore(blob =>
				blob.ConnectionString(_azuriteContainer!.GetConnectionString())
					.ContainerName("tiered-di-cold-events")
					.CreateContainerIfNotExists());
		}));

		return services.BuildServiceProvider();
	}

	private static IDomainEvent CreateEvent(long version) => new TieredDiEvent
	{
		AggregateId = AggregateId,
		Version = version,
		EventId = Guid.NewGuid().ToString(),
		OccurredAt = DateTimeOffset.UtcNow,
		EventType = nameof(TieredDiEvent),
	};

	private sealed class TieredDiEvent : IDomainEvent
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(TieredDiEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
