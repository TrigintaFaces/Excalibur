// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
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
		// Routed through the readiness wait because the FIRST data-plane call is the one that races
		// the emulator's extension startup; see WaitForDataPlaneReadyAsync.
		await WaitForDataPlaneReadyAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Creates the fixture-owned database, waiting out the emulator's data-plane startup.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>THE RACE.</b> <c>StartAsync</c> returns when the Testcontainers wait strategy is satisfied —
	/// the container is up and the gateway answers. The vNext emulator's data plane is served by a
	/// <c>pgcosmos</c> extension that is still initialising at that moment, so the first request is
	/// rejected with <c>503 ServiceUnavailable</c> and the reason string
	/// <c>"pgcosmos extension is still starting; retry request shortly"</c>. Container-ready and
	/// data-plane-ready are two different states and only the first one is waited on.
	/// </para>
	/// <para>
	/// <b>WHY THE EXISTING RETRIES DID NOT COVER IT.</b> <see cref="ContainerFixtureBase" />'s retry
	/// loop only re-attempts exceptions its classifier calls retriable — timeouts, cancellations and
	/// Docker faults. A <see cref="CosmosException" /> is none of those, so the fixture broke out on
	/// the first failure. The client's own <c>WithThrottlingRetryOptions</c> does not help either: it
	/// governs <c>429</c> throttling, not <c>503</c>. And restarting the container would be the wrong
	/// remedy regardless — the container is healthy, only the extension inside it is not yet up.
	/// </para>
	/// <para>
	/// <b>THE RETRY PREDICATE IS DELIBERATELY NARROW.</b> Only <c>503</c> is retried. A malformed
	/// connection string, a bad endpoint or an auth fault therefore fails on the first attempt with
	/// its own error instead of being masked for the whole timeout and then reported as a readiness
	/// timeout. Retrying everything would turn a five-second configuration bug into a two-minute
	/// mystery.
	/// </para>
	/// <para>
	/// <b>THE CAUSE IS PRESERVED.</b> On exhaustion this throws with the last <see cref="CosmosException" />
	/// as the inner exception. A readiness wait that reports only "timed out" would reproduce the
	/// original defect one layer up — the failure that started this was undiagnosable precisely
	/// because the real exception had been swallowed.
	/// </para>
	/// </remarks>
	private async Task WaitForDataPlaneReadyAsync(CancellationToken cancellationToken)
	{
		// Bounded by the caller's token, which ContainerFixtureBase already scopes to
		// ContainerStartTimeout — so this cannot outlive the fixture's own startup budget.
		var pollInterval = TimeSpan.FromSeconds(2);
		var attempts = 0;
		CosmosException? lastTransient = null;

		while (!cancellationToken.IsCancellationRequested)
		{
			attempts++;
			try
			{
				_ = await _client!.CreateDatabaseIfNotExistsAsync(DatabaseName, cancellationToken: cancellationToken)
					.ConfigureAwait(false);
				return;
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
			{
				lastTransient = ex;
			}

			try
			{
				// This is the backoff INSIDE a poll loop, not a sync-wait before an assertion: the
				// loop above polls the real condition (the data-plane call succeeding) and is
				// bounded by the caller's token. The delay only paces retries between polls.
				await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false); // delay-ok: poll-loop backoff, not a sync-wait
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}

		throw new InvalidOperationException(
			$"CosmosDB emulator data plane was not ready after {attempts} attempt(s): the emulator kept "
			+ "returning 503 ServiceUnavailable. The container started, so this is the emulator's internal "
			+ "startup exceeding the fixture's budget, not a Docker failure. See the inner exception.",
			lastTransient);
	}

	/// <summary>
	/// Deletes the per-run database (and its containers) to isolate this fixture's data.
	/// </summary>
	/// <summary>
	/// Deletes the snapshot CONTAINER, leaving the fixture-owned database intact.
	/// </summary>
	/// <remarks>
	/// Per-test teardown previously called <see cref="CleanupDatabaseAsync" />, which deletes the DATABASE —
	/// but the database and container are fixture-owned and created once for the whole class. The first
	/// test's teardown therefore destroyed the database out from under every test that followed, and all of
	/// them failed with "Database not found" rather than anything to do with snapshots. A per-test cleanup
	/// must only remove what that test created. The store recreates this container on next use because it is
	/// configured with CreateContainerIfNotExists.
	/// </remarks>
	public async Task CleanupContainerAsync()
	{
		if (_client is null)
		{
			return;
		}

		try
		{
			_ = await _client.GetDatabase(DatabaseName).GetContainer(ContainerName).DeleteContainerAsync()
				.ConfigureAwait(false);
		}
		catch (CosmosException)
		{
			// Best effort — already gone, or the emulator is shutting down.
		}
	}

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
