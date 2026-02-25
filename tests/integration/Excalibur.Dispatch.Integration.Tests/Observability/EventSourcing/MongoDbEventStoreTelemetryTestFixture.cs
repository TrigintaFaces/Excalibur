// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MsOptions = Microsoft.Extensions.Options.Options;
using Excalibur.Data.MongoDB.EventSourcing;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Testcontainers.MongoDb;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Test fixture for MongoDB EventStore telemetry integration testing.
/// Provides a MongoDB container with ActivitySource listeners for capturing telemetry data during tests.
/// </summary>
public sealed class MongoDbEventStoreTelemetryTestFixture : IAsyncLifetime, IDisposable
{
	private readonly MongoDbContainer _container;
	private readonly ActivityListener _activityListener;
	private readonly List<Activity> _recordedActivities = [];
	private readonly DateTimeOffset _creationTime = DateTimeOffset.UtcNow;
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStoreTelemetryTestFixture"/> class.
	/// </summary>
	public MongoDbEventStoreTelemetryTestFixture()
	{
		_container = new MongoDbBuilder()
			.WithImage("mongo:7.0")
			.WithName($"mongo-telemetry-test-{Guid.NewGuid():N}")
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
	/// Gets the connection string for the MongoDB container.
	/// </summary>
	public string ConnectionString => _container.GetConnectionString();

	/// <summary>
	/// Gets the database name for events.
	/// </summary>
	public string DatabaseName { get; } = "eventstore_telemetry_test";

	/// <summary>
	/// Gets the collection name for events.
	/// </summary>
	public string CollectionName { get; } = "event_store_events";

	/// <summary>
	/// Gets the counter collection name.
	/// </summary>
	public string CounterCollectionName { get; } = "event_store_counters";

	/// <summary>
	/// Gets a value indicating whether the fixture was successfully initialized.
	/// </summary>
	public bool IsInitialized { get; private set; }

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);
			// Note: MongoDbEventStore creates its own indexes on first use via EnsureInitializedAsync
			// We don't need to pre-create indexes here
			IsInitialized = true;
		}
		catch (Exception)
		{
			// Container may fail to start if Docker is not available
			IsInitialized = false;
		}
	}

	/// <summary>
	/// Creates a MongoDbEventStore configured for this container.
	/// </summary>
	public MongoDbEventStore CreateEventStore()
	{
		if (!IsInitialized)
		{
			throw new InvalidOperationException("Test fixture not initialized. MongoDB container may not be available.");
		}

		var options = MsOptions.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = ConnectionString,
			DatabaseName = DatabaseName,
			CollectionName = CollectionName,
			CounterCollectionName = CounterCollectionName,
		});

		return new MongoDbEventStore(
			options,
			NullLogger<MongoDbEventStore>.Instance);
	}

	/// <summary>
	/// Cleans up all items from the events and counter collections.
	/// </summary>
	public async Task CleanupCollectionAsync()
	{
		var client = new MongoClient(ConnectionString);
		var database = client.GetDatabase(DatabaseName);

		// Drop and let the store recreate on next test
		await database.DropCollectionAsync(CollectionName).ConfigureAwait(false);
		await database.DropCollectionAsync(CounterCollectionName).ConfigureAwait(false);
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
		ClearRecordedActivities();

		_disposed = true;
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await _container.DisposeAsync().ConfigureAwait(false);
	}
}
