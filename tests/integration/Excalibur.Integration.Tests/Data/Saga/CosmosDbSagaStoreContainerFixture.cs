// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using System.Text.Json;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

using Testcontainers.CosmosDb;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// CosmosDB Linux-emulator container fixture for the Cosmos saga-store optimistic-concurrency conformance
/// (e1tsq2, S853). Mirrors the event-store telemetry fixture's emulator client setup (gateway mode +
/// the emulator's self-signed cert via <c>HttpClientFactory</c>). Degrades gracefully
/// (<see cref="IsInitialized"/> = false) when the (heavy) emulator can't start — Cosmos emulator support
/// is limited on some CI hosts.
/// </summary>
public sealed class CosmosDbSagaStoreContainerFixture : IAsyncLifetime, IDisposable
{
	private readonly CosmosDbContainer _container;
	private CosmosClient? _client;
	private bool _disposed;

	public CosmosDbSagaStoreContainerFixture()
	{
		_container = new CosmosDbBuilder()
			// Pinned by digest, not tag: a tag is mutable, so a later run can silently receive a different
			// image than the one this suite's evidence was measured on. The digest cannot move.
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:a8b93e25520e999d867ed3949e7de7f4ff3ddab23ca95fa6f90230de5dd9729b")
			.WithName($"cosmosdb-saga-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();
	}

	/// <summary>Gets a value indicating whether the emulator started + the database was created.</summary>
	public bool IsInitialized { get; private set; }

	/// <summary>Diagnostic: the init failure reason when the emulator could not start.</summary>
	/// <remarks>
	/// Without this the init exception was discarded entirely, so a fixture that failed for one reason
	/// (emulator absent, port unmapped, gateway died mid-handshake) was indistinguishable from any other.
	/// Mirrors the transactional-inbox fixture, which already captured it.
	/// </remarks>
	public string? InitError { get; private set; }

	/// <summary>Gets the emulator-configured Cosmos client (gateway mode, emulator cert).</summary>
	/// <remarks>
	/// Throws rather than handing back an unusable client. Previously the partially-built client stayed
	/// assigned after a failed init and was disposed on teardown, so consumers reached it and surfaced
	/// <c>ObjectDisposedException: Accessing CosmosClient after it is disposed</c> — an error naming the
	/// DISPOSAL and saying nothing about the emulator failure that actually happened. Every test after the
	/// first then reported that instead of the real cause, which is what made a whole suite's failures
	/// unreadable. Failing here names the real reason at the point of use.
	/// </remarks>
	public CosmosClient Client =>
		_client ?? throw new InvalidOperationException(
			"The Cosmos saga fixture is not initialized, so no client exists. This is not a client-lifetime "
			+ "problem — the emulator never came up. Underlying initialization failure: "
			+ (InitError ?? "(none recorded — InitializeAsync did not run)"));

	/// <summary>
	/// Asserts that the emulator is available, throwing when it is not.
	/// </summary>
	/// <remarks>
	/// THE FIXTURE OWNS THE AVAILABILITY POLICY, so every suite on this fixture answers "what
	/// happens when the emulator is not there" the same way. Delegating the decision is what let
	/// two suites against this same emulator answer it in OPPOSITE ways, leaving the honesty of a
	/// given guarantee to depend on which convention its author happened to copy.
	/// <para>
	/// The policy is HARD FAILURE, not a skip: an un-run lock must not contribute a pass it did
	/// not earn. A host that genuinely cannot run this belongs in the reviewed, expiring
	/// suppression list -- named, owned and evidenced -- not in a per-test skip nobody can audit.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">The emulator is not available.</exception>
	public void EnsureAvailable()
	{
		if (IsInitialized)
		{
			return;
		}

		throw new InvalidOperationException(
			"CosmosDbSagaStoreContainerFixture: the Cosmos emulator is not available, so this test cannot exercise the "
			+ "real system it exists to verify. Reporting a pass here would certify a guarantee "
			+ "nothing checked. Underlying initialization failure: "
			+ (InitError ?? "(none recorded -- InitializeAsync did not run)"));
	}

	/// <summary>Gets the emulator connection string (also fed to options to satisfy Validate()).</summary>
	public string ConnectionString => _container.GetConnectionString();

	/// <summary>Gets the emulator HttpClient (trusts the self-signed cert) for the options factory.</summary>
	public HttpClient EmulatorHttpClient => _container.HttpClient;

	/// <summary>Gets the saga database name.</summary>
	public string DatabaseName { get; } = "excalibur";

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);

			var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
			var client = new CosmosClientBuilder(_container.GetConnectionString())
				.WithConnectionModeGateway()
				.WithRequestTimeout(TimeSpan.FromSeconds(120))
				.WithThrottlingRetryOptions(TimeSpan.FromSeconds(30), 9)
				.WithHttpClientFactory(() => _container.HttpClient)
				.WithSystemTextJsonSerializerOptions(json)
				.Build();

			// The injected-client store path does NOT create the database — the fixture owns that.
			await WaitForDataPlaneReadyAsync(client).ConfigureAwait(false);

			// Published ONLY once the database round-trip has succeeded. Assigning before this point is
			// what let a client belonging to a failed init escape to consumers.
			_client = client;
			IsInitialized = true;
		}
		catch (Exception ex)
		{
			// Emulator may fail to start on constrained CI hosts. Record the reason — discarding it is what
			// made every downstream failure in this suite unreadable.
			IsInitialized = false;
			InitError = ex.ToString();
			_client = null;
		}
	}

	/// <summary>
	/// Polls the data plane until it accepts a request, rather than assuming a started container is a
	/// ready one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>THE RACE.</b> <c>StartAsync</c> returns when the Testcontainers wait strategy is satisfied — the
	/// container is up and the gateway answers. The vNext emulator's data plane is served by a
	/// <c>pgcosmos</c> extension still initialising at that moment, so the first request is rejected with
	/// <c>503 ServiceUnavailable</c> and the reason string <c>"pgcosmos extension is still starting; retry
	/// request shortly"</c>. Container-ready and data-plane-ready are two different states and only the
	/// first one was waited on.
	/// </para>
	/// <para>
	/// <b>WHY NOTHING ELSE COVERED IT.</b> This fixture does not derive from <c>ContainerFixtureBase</c>,
	/// whose retry classifier already treats that message as retriable, so it inherited none of that
	/// protection. The client's own <c>WithThrottlingRetryOptions</c> does not help either: it governs
	/// <c>429</c> throttling, not <c>503</c>. And because this fixture's availability policy is a
	/// deliberate HARD FAILURE, losing the race did not degrade one test — it failed every test in the
	/// suite with an error naming the emulator rather than the timing.
	/// </para>
	/// <para>
	/// <b>THE PREDICATE IS DELIBERATELY NARROW.</b> Only <c>503</c> is retried, so a malformed connection
	/// string, a bad endpoint or an auth fault still fails on its first attempt with its own error instead
	/// of being masked for the whole budget and then reported as a readiness timeout. On exhaustion the
	/// last <see cref="CosmosException"/> is preserved as the inner exception — a wait that reported only
	/// "timed out" would reproduce the undiagnosable failure this fixture was already once repaired for.
	/// </para>
	/// </remarks>
	private async Task WaitForDataPlaneReadyAsync(CosmosClient client)
	{
		using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(120));
		var pollInterval = TimeSpan.FromSeconds(2);
		var attempts = 0;
		CosmosException? lastTransient = null;

		while (!budget.IsCancellationRequested)
		{
			attempts++;
			try
			{
				_ = await client.CreateDatabaseIfNotExistsAsync(DatabaseName, cancellationToken: budget.Token)
					.ConfigureAwait(false);
				return;
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
			{
				lastTransient = ex;
			}

			try
			{
				// Backoff INSIDE a poll loop, not a sync-wait before an assertion: the loop polls the real
				// condition (the data-plane call succeeding) and is bounded by the budget above.
				await Task.Delay(pollInterval, budget.Token).ConfigureAwait(false); // delay-ok: poll-loop backoff, not a sync-wait
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

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		// Cleared as well as disposed: a disposed-but-reachable client is what turned an emulator outage
		// into a suite full of ObjectDisposedException. After teardown, Client throws a message naming the
		// real cause instead.
		_client?.Dispose();
		_client = null;
		_disposed = true;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		Dispose();

		try
		{
			var disposeTask = _container.DisposeAsync().AsTask();
			var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
			if (completed == disposeTask)
			{
				await disposeTask.ConfigureAwait(false);
			}
		}
		catch
		{
			// Best effort — allow the test host to exit cleanly.
		}
	}
}
