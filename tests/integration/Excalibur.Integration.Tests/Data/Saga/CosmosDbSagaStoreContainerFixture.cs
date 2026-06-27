// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	private bool _disposed;

	public CosmosDbSagaStoreContainerFixture()
	{
		_container = new CosmosDbBuilder()
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest")
			.WithName($"cosmosdb-saga-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();
	}

	/// <summary>Gets a value indicating whether the emulator started + the database was created.</summary>
	public bool IsInitialized { get; private set; }

	/// <summary>Gets the emulator-configured Cosmos client (gateway mode, emulator cert).</summary>
	public CosmosClient Client { get; private set; } = null!;

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
			Client = new CosmosClientBuilder(_container.GetConnectionString())
				.WithConnectionModeGateway()
				.WithRequestTimeout(TimeSpan.FromSeconds(120))
				.WithThrottlingRetryOptions(TimeSpan.FromSeconds(30), 9)
				.WithHttpClientFactory(() => _container.HttpClient)
				.WithSystemTextJsonSerializerOptions(json)
				.Build();

			// The injected-client store path does NOT create the database — the fixture owns that.
			_ = await Client.CreateDatabaseIfNotExistsAsync(DatabaseName).ConfigureAwait(false);

			IsInitialized = true;
		}
		catch (Exception)
		{
			// Emulator may fail to start on constrained CI hosts.
			IsInitialized = false;
		}
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
