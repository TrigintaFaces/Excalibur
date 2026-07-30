// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

using Testcontainers.CosmosDb;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Cosmos DB Linux-emulator container fixture for the Cosmos scoped-transactional-inbox exactly-once lock
/// (bd-etm9ih / B2, S874). Boots the emulator, then creates the inbox container with the <c>/handler_type</c>
/// partition-key path the store expects — so the framework-built store (which builds its OWN client from the
/// options connection string + the emulator cert-bypass <c>HttpClientFactory</c>) can read/write it.
/// </summary>
/// <remarks>
/// The emulator is heavy and unavailable on some CI hosts; <see cref="IsInitialized"/> degrades to
/// <see langword="false"/> when it can't start. The lock treats that as a HARD failure
/// (<c>IsInitialized.ShouldBeTrue</c>) rather than a silent skip — a real-infra exactly-once lock that never
/// runs is exactly the coverage gap that let the <c>UpsertItem</c> double-execute ship.
/// </remarks>
public sealed class CosmosDbTransactionalInboxExactlyOnceFixture : IAsyncLifetime, IDisposable
{
	private readonly CosmosDbContainer _container;
	private bool _disposed;

	public CosmosDbTransactionalInboxExactlyOnceFixture()
	{
		_container = new CosmosDbBuilder()
			// Pinned by digest, not tag: a tag is mutable, so a later run can silently receive a different
			// image than the one this suite's evidence was measured on. The digest cannot move.
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:a8b93e25520e999d867ed3949e7de7f4ff3ddab23ca95fa6f90230de5dd9729b")
			.WithName($"cosmosdb-txinbox-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();
	}

	/// <summary>Gets a value indicating whether the emulator started + the container was created.</summary>
	public bool IsInitialized { get; private set; }

	/// <summary>Diagnostic: the init failure reason when the emulator could not start.</summary>
	public string? InitError { get; private set; }

	/// <summary>Gets the emulator connection string (fed to the store options).</summary>
	public string ConnectionString => _container.GetConnectionString();

	/// <summary>Gets the cert-bypass HttpClient factory the store needs to reach the emulator.</summary>
	public Func<HttpClient> HttpClientFactory => () => _container.HttpClient;

	/// <summary>Gets the emulator-configured client the test uses to read committed state back.</summary>
	public CosmosClient Client { get; private set; } = null!;

	public string DatabaseName { get; } = "excalibur";

	public string ContainerName { get; } = "inbox-messages";

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);

			var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
			Client = new CosmosClientBuilder(_container.GetConnectionString())
				.WithConnectionModeGateway()
				.WithRequestTimeout(TimeSpan.FromSeconds(120))
				.WithThrottlingRetryOptions(TimeSpan.FromSeconds(30), 9)
				.WithHttpClientFactory(() => _container.HttpClient)
				.WithSystemTextJsonSerializerOptions(json)
				.Build();

			// The emulator reports the port open before all partitions finish booting, so the first control-plane
			// calls can fail transiently ("response ended prematurely"). Retry readiness on a bounded window.
			await CreateDatabaseAndContainerWithReadinessRetryAsync().ConfigureAwait(false);

			IsInitialized = true;
		}
		catch (Exception ex)
		{
			// Emulator may fail to start on constrained CI hosts — surfaced as a hard failure by the lock.
			IsInitialized = false;
			InitError = ex.ToString();
		}
	}

	private async Task CreateDatabaseAndContainerWithReadinessRetryAsync()
	{
		const int maxAttempts = 40;
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				var database = await Client.CreateDatabaseIfNotExistsAsync(DatabaseName).ConfigureAwait(false);
				_ = await database.Database.CreateContainerIfNotExistsAsync(
					new ContainerProperties(ContainerName, "/handler_type")).ConfigureAwait(false);
				return;
			}
			catch (Exception) when (attempt < maxAttempts)
			{
				// Emulator partitions still booting — wait and retry within the bounded window.
				await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
			}
		}
	}

	/// <summary>Counts committed side-effect docs carrying <paramref name="marker"/>, read on the fixture client.</summary>
	public async Task<int> CountSideEffectsAsync(string marker, string partitionKey)
	{
		var container = Client.GetContainer(DatabaseName, ContainerName);
		var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.marker = @m")
			.WithParameter("@m", marker);
		using var iterator = container.GetItemQueryIterator<int>(
			query,
			requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) });
		var total = 0;
		while (iterator.HasMoreResults)
		{
			foreach (var count in await iterator.ReadNextAsync().ConfigureAwait(false))
			{
				total += count;
			}
		}

		return total;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		Client?.Dispose();
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
