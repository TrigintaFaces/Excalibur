// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

using Testcontainers.CosmosDb;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// CosmosDB Linux-emulator container fixture for the Cosmos DB <c>IEventStore</c> conformance tests.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the Cosmos snapshot/saga fixtures' emulator client setup (gateway mode + the emulator's
/// self-signed cert via <c>HttpClientFactory</c>). The <c>CosmosDbEventStore</c> accepts a
/// consumer-supplied <see cref="CosmosClient"/> and hardcodes its database name to <c>"events"</c>,
/// self-creating the events container inside that database. The fixture therefore owns the client and
/// the per-run database, and exposes a unique container name per test for isolation.
/// </para>
/// <para>
/// Default-serializer axis: the client is built WITHOUT a System.Text.Json serializer override, so it
/// uses the Cosmos SDK v3 DEFAULT (Newtonsoft) serializer — the exact surface a consumer who supplies a
/// raw client gets. The event document is dual-mapped (<c>[JsonPropertyName]</c> + Newtonsoft
/// <c>[JsonProperty]</c>), so the round-trip must succeed under this default path. Exercising the default
/// serializer (not a hand-configured STJ client) is what makes this a faithful real-infra lock.
/// </para>
/// <para>
/// The fixture does NOT degrade gracefully: real-infra conformance is never skipped, so a missing
/// emulator surfaces as a failure rather than a silent pass.
/// </para>
/// </remarks>
public sealed class CosmosDbEventStoreContainerFixture : ContainerFixtureBase
{
	private CosmosDbContainer? _container;

	/// <summary>
	/// Gets the database name the store hardcodes (<c>CosmosDbEventStore</c> uses
	/// <c>_cosmosClient.GetDatabase("events")</c>); the fixture owns/creates it.
	/// </summary>
	public string DatabaseName { get; } = "events";

	/// <summary>
	/// Gets the emulator-configured Cosmos client built with the SDK DEFAULT (Newtonsoft) serializer —
	/// the same surface a consumer supplying a raw client would have. Fed directly to the store ctor.
	/// </summary>
	public CosmosClient Client { get; private set; } = null!;

	/// <summary>
	/// Gets the emulator connection string.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the emulator HttpClient (trusts the self-signed cert).
	/// </summary>
	public HttpClient EmulatorHttpClient => _container?.HttpClient
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(10);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new CosmosDbBuilder()
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest")
			.WithName($"cosmosdb-eventstore-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// DEFAULT serializer on purpose: no .WithSystemTextJsonSerializerOptions(...). The Cosmos SDK v3
		// default is Newtonsoft, which is what a consumer-supplied client uses. The dual-mapped event
		// document must round-trip under this default path.
		Client = new CosmosClientBuilder(_container.GetConnectionString())
			.WithConnectionModeGateway()
			.WithRequestTimeout(TimeSpan.FromSeconds(120))
			.WithThrottlingRetryOptions(TimeSpan.FromSeconds(30), 9)
			.WithHttpClientFactory(() => _container.HttpClient)
			.Build();

		// The store's injected-client path does NOT create the database — the fixture owns that.
		_ = await Client.CreateDatabaseIfNotExistsAsync(DatabaseName, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Deletes a per-test events container to isolate each conformance test's data.
	/// </summary>
	/// <param name="containerName">The container the store self-created for the test.</param>
	public async Task DeleteContainerAsync(string containerName)
	{
		if (Client is null)
		{
			return;
		}

		try
		{
			_ = await Client.GetDatabase(DatabaseName).GetContainer(containerName).DeleteContainerAsync()
				.ConfigureAwait(false);
		}
		catch (CosmosException)
		{
			// Best effort — never created (e.g., empty-stream test) or already gone.
		}
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			Client?.Dispose();

			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}
