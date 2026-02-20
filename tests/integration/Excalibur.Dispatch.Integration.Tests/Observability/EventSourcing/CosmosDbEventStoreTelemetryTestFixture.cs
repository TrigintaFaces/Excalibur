// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MsOptions = Microsoft.Extensions.Options.Options;
using System.Text.Json;

using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.Observability;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Testcontainers.CosmosDb;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Test fixture for CosmosDb EventStore telemetry integration testing.
/// Provides a CosmosDb emulator container with ActivitySource listeners for capturing telemetry data during tests.
/// </summary>
/// <remarks>
/// Note: The CosmosDb Linux emulator has known limitations on CI environments (GitHub Actions, Azure DevOps).
/// Tests using this fixture may be skipped in CI environments.
/// </remarks>
public sealed class CosmosDbEventStoreTelemetryTestFixture : IAsyncLifetime, IDisposable
{
	private readonly CosmosDbContainer _container;
	private readonly ActivityListener _activityListener;
	private readonly List<Activity> _recordedActivities = [];
	private readonly DateTimeOffset _creationTime = DateTimeOffset.UtcNow;
	private bool _disposed;

	private CosmosClient? _cosmosClient;
	private Database? _database;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventStoreTelemetryTestFixture"/> class.
	/// </summary>
	public CosmosDbEventStoreTelemetryTestFixture()
	{
		_container = new CosmosDbBuilder()
			.WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest")
			.WithName($"cosmosdb-telemetry-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		// Set up activity listener for capturing spans
		_activityListener = new ActivityListener
		{
			ShouldListenTo = source =>
				source.Name == EventSourcingActivitySource.Name ||
				source.Name.StartsWith("Excalibur.", StringComparison.Ordinal) ||
				source.Name.StartsWith("Test.", StringComparison.Ordinal),

			Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
				ActivitySamplingResult.AllDataAndRecorded,

			ActivityStopped = activity =>
			{
				if (activity != null && activity.StartTimeUtc >= _creationTime.UtcDateTime)
				{
					lock (_recordedActivities)
					{
						_recordedActivities.Add(activity);
					}
				}
			},
		};

		ActivitySource.AddActivityListener(_activityListener);
	}

	/// <summary>
	/// Gets a value indicating whether the fixture was successfully initialized.
	/// </summary>
	public bool IsInitialized { get; private set; }

	/// <summary>
	/// Gets the database name for events.
	/// </summary>
	public string DatabaseName { get; } = "events";

	/// <summary>
	/// Gets the container name for events.
	/// </summary>
	public string ContainerName { get; } = "events";

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);

			// Configure System.Text.Json options with proper property naming
			var jsonSerializerOptions = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			};

			// Create Cosmos client with emulator settings and System.Text.Json serialization
			_cosmosClient = new CosmosClientBuilder(_container.GetConnectionString())
				.WithConnectionModeGateway()
				.WithHttpClientFactory(() => _container.HttpClient)
				.WithSystemTextJsonSerializerOptions(jsonSerializerOptions)
				.Build();

			// Create database
			var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName)
				.ConfigureAwait(false);
			_database = databaseResponse.Database;

			IsInitialized = true;
		}
		catch (Exception)
		{
			// Container may fail to start in CI environments
			IsInitialized = false;
		}
	}

	/// <summary>
	/// Creates a CosmosDbEventStore configured for this container.
	/// </summary>
	public CosmosDbEventStore CreateEventStore()
	{
		if (!IsInitialized || _cosmosClient == null)
		{
			throw new InvalidOperationException("Test fixture not initialized. CosmosDb emulator may not be available.");
		}

		var options = MsOptions.Create(new CosmosDbEventStoreOptions
		{
			EventsContainerName = ContainerName,
			PartitionKeyPath = "/streamId",
			CreateContainerIfNotExists = true,
			UseTransactionalBatch = false, // Use sequential for simpler concurrency testing
			ContainerThroughput = 400,
		});

		return new CosmosDbEventStore(
			_cosmosClient,
			options,
			NullLogger<CosmosDbEventStore>.Instance);
	}

	/// <summary>
	/// Cleans up all items from the events container.
	/// </summary>
	public async Task CleanupContainerAsync()
	{
		if (!IsInitialized || _database == null)
		{
			return;
		}

		try
		{
			// Drop and let the store recreate on next test
			var container = _database.GetContainer(ContainerName);
			_ = await container.DeleteContainerAsync().ConfigureAwait(false);
		}
		catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			// Container doesn't exist, that's fine
		}
	}

	/// <summary>
	/// Gets all activities recorded since fixture creation or last <see cref="ClearRecordedActivities"/> call.
	/// </summary>
	public IReadOnlyList<Activity> GetRecordedActivities()
	{
		lock (_recordedActivities)
		{
			return _recordedActivities.ToList().AsReadOnly();
		}
	}

	/// <summary>
	/// Clears all recorded activity data.
	/// </summary>
	public void ClearRecordedActivities()
	{
		lock (_recordedActivities)
		{
			_recordedActivities.Clear();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_activityListener?.Dispose();
		_cosmosClient?.Dispose();
		ClearRecordedActivities();

		_disposed = true;
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await _container.DisposeAsync().ConfigureAwait(false);
	}
}
