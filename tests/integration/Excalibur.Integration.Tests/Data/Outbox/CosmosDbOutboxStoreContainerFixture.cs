// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

using Testcontainers.CosmosDb;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Cosmos DB Linux-emulator container fixture for the Cosmos <c>ICloudNativeOutboxStore</c>
/// real-infrastructure tests.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>CosmosDbSnapshotStoreContainerFixture</c>, including its data-plane readiness wait: the
/// container being up is not the same state as the emulator's data plane being up, and the first request
/// after <c>StartAsync</c> is the one that races the extension's startup.
/// </para>
/// <para>
/// The fixture owns the per-run database; the store under test builds its own client from the connection
/// string and self-creates its container inside that database.
/// </para>
/// <para>
/// Does NOT degrade gracefully: a missing emulator surfaces as a failure rather than a silent pass.
/// </para>
/// </remarks>
public sealed class CosmosDbOutboxStoreContainerFixture : ContainerFixtureBase
{
	private CosmosDbContainer? _container;
	private CosmosClient? _client;

	/// <summary>
	/// Gets the per-run database name. The store's connection-string path does not create a database.
	/// </summary>
	public string DatabaseName { get; } = $"outbox_{Guid.NewGuid():N}";

	/// <summary>
	/// Gets the emulator's HttpClient, which the store needs to reach the emulator at all.
	/// </summary>
	public HttpClient EmulatorHttpClient => _container?.HttpClient
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the emulator connection string fed to the store options.
	/// </summary>
	public string ConnectionString => _container is null
		? throw new InvalidOperationException("Container not initialized")
		: _container.GetConnectionString();

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(10);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new CosmosDbBuilder()
			// Pinned by digest, not tag: a tag is mutable, so a later run can silently receive a different
			// image than the one this suite's evidence was measured on.
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:a8b93e25520e999d867ed3949e7de7f4ff3ddab23ca95fa6f90230de5dd9729b")
			.WithName($"cosmosdb-outbox-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		_client = new CosmosClientBuilder(_container.GetConnectionString())
			.WithConnectionModeGateway()
			.WithRequestTimeout(TimeSpan.FromSeconds(120))
			.WithThrottlingRetryOptions(TimeSpan.FromSeconds(30), 9)
			.WithHttpClientFactory(() => _container.HttpClient)
			.WithSystemTextJsonSerializerOptions(json)
			.Build();

		await WaitForDataPlaneReadyAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Reads a container's provisioned default time-to-live, so a test can assert what the store provisioned
	/// rather than wait out an expiry window.
	/// </summary>
	/// <param name="containerName">The container to inspect.</param>
	/// <returns>The container's <c>DefaultTimeToLive</c>, or <see langword="null"/> when TTL is off.</returns>
	public async Task<int?> ReadContainerDefaultTtlAsync(string containerName)
	{
		var response = await _client!.GetDatabase(DatabaseName).GetContainer(containerName)
			.ReadContainerAsync().ConfigureAwait(false);

		return response.Resource.DefaultTimeToLive;
	}

	/// <summary>
	/// Reads a stored document's server-assigned <c>_etag</c>, bypassing the store's own mapping.
	/// </summary>
	/// <param name="containerName">The container holding the document.</param>
	/// <param name="id">The document id.</param>
	/// <param name="partitionKeyValue">The document's partition key.</param>
	/// <returns>The server's concurrency token for that document.</returns>
	public async Task<string> ReadDocumentETagAsync(string containerName, string id, string partitionKeyValue)
	{
		var response = await _client!.GetDatabase(DatabaseName).GetContainer(containerName)
			.ReadItemAsync<System.Text.Json.JsonElement>(id, new PartitionKey(partitionKeyValue))
			.ConfigureAwait(false);

		return response.ETag;
	}

	/// <summary>
	/// Reads a single stored document's raw <c>ttl</c> property, bypassing the store's own mapping.
	/// </summary>
	/// <param name="containerName">The container holding the document.</param>
	/// <param name="id">The document id.</param>
	/// <param name="partitionKeyValue">The document's partition key.</param>
	/// <returns>The stored <c>ttl</c>, or <see langword="null"/> when the property is absent.</returns>
	public async Task<int?> ReadDocumentTtlAsync(string containerName, string id, string partitionKeyValue)
	{
		var response = await _client!.GetDatabase(DatabaseName).GetContainer(containerName)
			.ReadItemAsync<System.Text.Json.JsonElement>(id, new PartitionKey(partitionKeyValue))
			.ConfigureAwait(false);

		return response.Resource.TryGetProperty("ttl", out var ttl) && ttl.ValueKind == System.Text.Json.JsonValueKind.Number
			? ttl.GetInt32()
			: null;
	}

	/// <summary>
	/// Deletes one container from the fixture-owned database, leaving the database intact.
	/// </summary>
	/// <param name="containerName">The container to delete.</param>
	/// <returns>A task that completes when the container is gone, or was already gone.</returns>
	public async Task CleanupContainerAsync(string containerName)
	{
		if (_client is null || string.IsNullOrEmpty(containerName))
		{
			return;
		}

		try
		{
			_ = await _client.GetDatabase(DatabaseName).GetContainer(containerName).DeleteContainerAsync()
				.ConfigureAwait(false);
		}
		catch (CosmosException)
		{
			// Best effort — already gone, or the emulator is shutting down.
		}
	}

	/// <summary>
	/// Creates the fixture-owned database, waiting out the emulator's data-plane startup.
	/// </summary>
	/// <remarks>
	/// The predicate is deliberately narrow — only <c>503</c> is retried — so a bad connection string or an
	/// auth fault fails immediately with its own error rather than being masked for the whole budget and
	/// then reported as a readiness timeout. On exhaustion the last transient is preserved as the inner
	/// exception, because a wait that reports only "timed out" is undiagnosable.
	/// </remarks>
	/// <param name="cancellationToken">Bounds the wait; already scoped to the fixture's start budget.</param>
	private async Task WaitForDataPlaneReadyAsync(CancellationToken cancellationToken)
	{
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
