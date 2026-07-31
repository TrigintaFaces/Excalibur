// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

using Testcontainers.CosmosDb;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// CosmosDB Linux-emulator container fixture for the Cosmos DB SnapshotStore conformance tests.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the Cosmos saga-store fixture's emulator client setup (gateway mode + the emulator's
/// self-signed cert via <c>HttpClientFactory</c>). The fixture owns the Cosmos client used to create
/// and tear down a per-run database; the store under test creates its own client (from the connection
/// string) and self-creates the container inside that database.
/// </para>
/// <para>
/// The fixture does NOT degrade gracefully: real-infra conformance is never skipped, so a missing
/// emulator surfaces as a failure rather than a silent pass.
/// </para>
/// </remarks>
public sealed class CosmosDbSnapshotStoreContainerFixture : ContainerFixtureBase
{
	private CosmosDbContainer? _container;
	private CosmosClient? _client;

	/// <summary>
	/// Gets the per-run database name (the store does not create the database; the fixture owns it).
	/// </summary>
	public string DatabaseName { get; } = $"snapshots_{Guid.NewGuid():N}";

	/// <summary>
	/// Gets the per-run container name the store self-creates for snapshots.
	/// </summary>
	public string ContainerName { get; } = $"snapshots_{Guid.NewGuid():N}";

	/// <summary>
	/// Gets the emulator connection string fed to the store options (the store builds its own client from it).
	/// </summary>
	public string ConnectionString => _container is null
		? throw new InvalidOperationException("Container not initialized")
		: _container.GetConnectionString();

	/// <summary>
	/// Gets the emulator HttpClient (trusts the self-signed cert) for the store's options factory.
	/// </summary>
	public HttpClient EmulatorHttpClient => _container?.HttpClient
		?? throw new InvalidOperationException("Container not initialized");

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(10);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new CosmosDbBuilder()
			// Pinned by digest, not tag: a tag is mutable, so a later run can silently receive a different
			// image than the one this suite's evidence was measured on. The digest cannot move.
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:a8b93e25520e999d867ed3949e7de7f4ff3ddab23ca95fa6f90230de5dd9729b")
			.WithName($"cosmosdb-snapshotstore-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		// The vNext emulator serves plain HTTP on 8081 and needs no self-signed-cert bypass, so the
		// HttpClientFactory dance the old emulator required is gone. The superseded image died
		// during the gateway handshake on CI and locally ("The response ended prematurely"), taking
		// every Cosmos arm down at fixture init; vNext reports Gateway=OK and is ~1.5GB smaller.
		_client = new CosmosClientBuilder(_container.GetConnectionString())
			.WithConnectionModeGateway()
			.WithRequestTimeout(TimeSpan.FromSeconds(120))
			.WithThrottlingRetryOptions(TimeSpan.FromSeconds(30), 9)
			.WithHttpClientFactory(() => _container.HttpClient)
			.WithSystemTextJsonSerializerOptions(json)
			.Build();

		// The store's connection-string path does NOT create the database — the fixture owns that.
		_ = await _client.CreateDatabaseIfNotExistsAsync(DatabaseName, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Deletes the per-run database (and its containers) to isolate this fixture's data.
	/// </summary>
	public async Task CleanupDatabaseAsync()
	{
		if (_client is null)
		{
			return;
		}

		try
		{
			_ = await _client.GetDatabase(DatabaseName).DeleteAsync().ConfigureAwait(false);
		}
		catch (CosmosException)
		{
			// Best effort — already gone or emulator shutting down.
		}
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			// CLEARED as well as disposed. A disposed-but-reachable client is what turns an emulator
			// failure into a suite full of ObjectDisposedException: `CleanupDatabaseAsync` guards with
			// `if (_client is null) return;`, which PASSES for a disposed instance and then uses it. The
			// resulting error names the DISPOSAL and says nothing about the emulator failure that actually
			// happened, so every test after the first reports the mask instead of the cause — which is
			// what made this suite's failures unreadable. The sibling saga fixture already carries this
			// fix and documents the same reasoning.
			_client?.Dispose();
			_client = null;

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
