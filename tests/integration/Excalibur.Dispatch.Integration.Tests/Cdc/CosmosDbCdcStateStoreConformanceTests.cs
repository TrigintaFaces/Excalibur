// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.CosmosDb;
using Excalibur.Testing.Conformance;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.CosmosDb;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.Cdc;

/// <summary>
/// Emulator container fixture for the CosmosDb CDC state-store conformance suite. Extends
/// <see cref="ContainerFixtureBase"/> so a missing emulator surfaces as a hard failure (never a silent
/// skip). Mirrors the image pin and builder shape already used for the CosmosDb EventStore telemetry
/// fixture in this project, so this suite starts from the same known-working emulator image.
/// </summary>
#pragma warning disable CA1812 // Instantiated by the xUnit test runner via IClassFixture<T>.
public sealed class CosmosDbCdcContainerFixture : ContainerFixtureBase
{
	/// <summary>
	/// The database every arm's container is created beneath.
	/// </summary>
	public const string DatabaseId = "excalibur_cdc_conformance";

	private const int DatabaseCreateAttempts = 12;

	private CosmosDbContainer? _container;
	private CosmosClient? _client;

	/// <summary>
	/// Gets the emulator-facing client handed to every store this suite builds.
	/// </summary>
	/// <remarks>
	/// One client shared by every arm, for two reasons. It is the configuration the connection-string
	/// constructor cannot express — Gateway mode and the container's certificate-trusting
	/// <c>HttpClient</c> — without which nothing here reaches the emulator at all. And a single instance
	/// handed to many stores is the shape a real host uses, so the store's promise not to dispose a client
	/// it was given is under test for the whole run rather than asserted in isolation: were it broken, the
	/// first store disposed would take the rest of the suite down with it.
	/// <para>
	/// Deliberately built with NO serializer configured, so the SDK v3 default (Newtonsoft, no naming
	/// policy) is what serializes the state document. Configuring System.Text.Json here would hide exactly
	/// the defect the document's dual annotation exists to prevent.
	/// </para>
	/// </remarks>
	public CosmosClient Client => _client
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the emulator connection string handed to <see cref="CosmosDbCdcStateStoreOptions.ConnectionString"/>.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new CosmosDbBuilder()
			// Pinned by digest, not tag: a tag is mutable, so a later run can silently receive a different
			// image than the one this suite's evidence was measured on. Same digest already used by this
			// project's CosmosDb EventStore telemetry fixture.
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:a8b93e25520e999d867ed3949e7de7f4ff3ddab23ca95fa6f90230de5dd9729b")
			.WithName($"cosmosdb-cdc-conformance-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		_client = new CosmosClientBuilder(_container.GetConnectionString())
			.WithConnectionModeGateway()
			.WithRequestTimeout(TimeSpan.FromSeconds(120))
			.WithThrottlingRetryOptions(TimeSpan.FromSeconds(30), 9)
			.WithHttpClientFactory(() => _container.HttpClient)
			.Build();

		// The store provisions its container but never its database -- it calls GetDatabase and creates only
		// the container beneath it, so an absent database surfaces as a 404 from every arm. Creating it here
		// is the fixture supplying what a real deployment supplies; a store that created databases would be
		// claiming an authority hosts generally do not grant it.
		// Retried because a freshly-started emulator accepts connections slightly before it will serve them.
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				_ = await _client.CreateDatabaseIfNotExistsAsync(
					DatabaseId,
					cancellationToken: cancellationToken).ConfigureAwait(false);
				break;
			}
			catch (CosmosException) when (attempt < DatabaseCreateAttempts)
			{
				await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException) when (attempt < DatabaseCreateAttempts)
			{
				await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		_client?.Dispose();

		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
#pragma warning restore CA1812

/// <summary>
/// Runs the shared CDC state-store conformance kit against the REAL <see cref="CosmosDbCdcStateStore"/> on
/// a CosmosDb emulator container.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CdcProviderConformanceTestKit"/> had no CosmosDb deriver, so every arm of the kit had never
/// exercised the base64 continuation-token round-trip, the point-read-by-<c>processorName</c> partition
/// key, or the store's own <see cref="CosmosDbCdcStateStoreOptions.CreateContainerIfNotExists"/>
/// provisioning against a real server.
/// </para>
/// <para>
/// The store is handed the fixture's <see cref="Microsoft.Azure.Cosmos.CosmosClient"/> rather than left to
/// build one from <see cref="CosmosDbCdcStateStoreOptions.ConnectionString"/>. That is not a convenience:
/// the emulator presents a self-signed certificate and answers over Gateway, neither of which a connection
/// string can express, so a store that builds its own client cannot reach this container and every arm
/// below would report on a conversation that never happened. Supplying the client is what makes this suite
/// measure the store instead of measuring its own unreachability.
/// </para>
/// <para>
/// Each arm gets a freshly-named container (via a new <see cref="CosmosDbCdcStateStoreOptions.ContainerId"/>
/// per <see cref="CreateStateStoreAsync"/> call), because the kit's empty-store arm requires a store with
/// no items and the fixture's emulator is shared across every <c>[Fact]</c> in this class.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Infrastructure", TestInfrastructure.CosmosDb)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CosmosDbCdcStateStoreConformanceTests
	: CdcProviderConformanceTestKit, IClassFixture<CosmosDbCdcContainerFixture>
{
	private readonly CosmosDbCdcContainerFixture _fixture;

	public CosmosDbCdcStateStoreConformanceTests(CosmosDbCdcContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a CosmosDb emulator container must be available - real-infra CDC conformance is never skipped, "
			+ "because an arm that passes by being skipped is indistinguishable from one that passed by "
			+ "working.");

		// No ConnectionString: the store is handed a client, so it never reads one. Omitting it rather than
		// supplying it and letting it go unused is what puts the waived-connection-string path under test on
		// real infrastructure -- were the waiver wrong, every arm would fail at construction.
		var options = MsOptions.Create(new CosmosDbCdcStateStoreOptions
		{
			DatabaseId = CosmosDbCdcContainerFixture.DatabaseId,
			ContainerId = $"cdc-state-{Guid.NewGuid():N}",
			CreateContainerIfNotExists = true,
		});

		ICdcStateStore store = new CosmosDbCdcStateStore(
			_fixture.Client,
			options,
			NullLogger<CosmosDbCdcStateStore>.Instance);
		return Task.FromResult(store);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Built through <see cref="CosmosDbCdcPosition"/> rather than as a free-form token, so the expected
	/// value is exactly what the store will hand back — <see cref="CosmosDbCdcPosition"/> IS a
	/// <see cref="ChangePosition"/>, so no lossy round-trip through <c>ToToken()</c>/<c>FromContinuationToken</c>
	/// happens before the store's explicit <see cref="ICdcStateStore.SavePositionAsync"/> sees it.
	/// </remarks>
	protected override ChangePosition CreateTestPosition(int index) =>
		CosmosDbCdcPosition.FromContinuationToken($"ct-{index:D6}");

	[Fact] public Task SaveAndGetPosition_RoundTrips_Test() => SaveAndGetPosition_RoundTrips();
	[Fact] public Task GetPosition_NoCheckpoint_ReturnsNull_Test() => GetPosition_NoCheckpoint_ReturnsNull();
	[Fact] public Task SavePosition_MultipleConsumers_Independent_Test() => SavePosition_MultipleConsumers_Independent();
	[Fact] public Task SavePosition_Overwrites_PreviousCheckpoint_Test() => SavePosition_Overwrites_PreviousCheckpoint();
	[Fact] public Task SavePosition_PreservesPositionValidity_Test() => SavePosition_PreservesPositionValidity();
	[Fact] public Task Resume_FromSavedCheckpoint_ReturnsCorrectPosition_Test() => Resume_FromSavedCheckpoint_ReturnsCorrectPosition();
	[Fact] public Task Resume_AfterDelete_ReturnsNull_Test() => Resume_AfterDelete_ReturnsNull();
	[Fact] public Task DeletePosition_ExistingCheckpoint_ReturnsTrue_Test() => DeletePosition_ExistingCheckpoint_ReturnsTrue();
	[Fact] public Task DeletePosition_NonExistentCheckpoint_ReturnsFalse_Test() => DeletePosition_NonExistentCheckpoint_ReturnsFalse();
	[Fact] public Task DeletePosition_DoesNotAffectOtherConsumers_Test() => DeletePosition_DoesNotAffectOtherConsumers();
	[Fact] public Task GetAllPositions_ReturnsAllConsumerCheckpoints_Test() => GetAllPositions_ReturnsAllConsumerCheckpoints();
	[Fact] public Task GetAllPositions_EmptyStore_ReturnsEmpty_Test() => GetAllPositions_EmptyStore_ReturnsEmpty();
	[Fact] public Task ConcurrentSavePosition_AllSucceed_Test() => ConcurrentSavePosition_AllSucceed();
	[Fact] public Task ConcurrentSavePosition_SameConsumer_LastWriteWins_Test() => ConcurrentSavePosition_SameConsumer_LastWriteWins();

	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
