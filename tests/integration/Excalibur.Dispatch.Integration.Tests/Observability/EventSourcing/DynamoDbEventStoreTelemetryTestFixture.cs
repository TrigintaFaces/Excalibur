// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MsOptions = Microsoft.Extensions.Options.Options;
using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.EventSourcing.DynamoDb;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Testcontainers.LocalStack;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Test fixture for DynamoDb EventStore telemetry integration testing.
/// Provides a LocalStack container with DynamoDB service and ActivitySource listeners
/// for capturing telemetry data during tests.
/// </summary>
public sealed class DynamoDbEventStoreTelemetryTestFixture : IAsyncLifetime, IDisposable
{
	private readonly LocalStackContainer _container;
	private readonly ActivityListener _activityListener;
	private readonly List<Activity> _recordedActivities = [];
	private readonly DateTimeOffset _creationTime = DateTimeOffset.UtcNow;
	private bool _disposed;

	private IAmazonDynamoDB? _dynamoDbClient;
	private IAmazonDynamoDBStreams? _streamsClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbEventStoreTelemetryTestFixture"/> class.
	/// </summary>
	public DynamoDbEventStoreTelemetryTestFixture()
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:latest")
			.WithName($"localstack-dynamodb-telemetry-test-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
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
	/// Gets the table name for events.
	/// </summary>
	public string TableName { get; } = "events";

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);

			// Create DynamoDB client pointing to LocalStack
			var dynamoDbConfig = new AmazonDynamoDBConfig
			{
				ServiceURL = _container.GetConnectionString(),
			};

			_dynamoDbClient = new AmazonDynamoDBClient(
				"test",
				"test",
				dynamoDbConfig);

			var streamsConfig = new AmazonDynamoDBStreamsConfig
			{
				ServiceURL = _container.GetConnectionString(),
			};

			_streamsClient = new AmazonDynamoDBStreamsClient(
				"test",
				"test",
				streamsConfig);

			IsInitialized = true;
		}
		catch (Exception)
		{
			// Container may fail to start in CI environments
			IsInitialized = false;
		}
	}

	/// <summary>
	/// Creates a DynamoDbEventStore configured for this container.
	/// </summary>
	public DynamoDbEventStore CreateEventStore()
	{
		if (!IsInitialized || _dynamoDbClient == null || _streamsClient == null)
		{
			throw new InvalidOperationException("Test fixture not initialized. LocalStack may not be available.");
		}

		var options = MsOptions.Create(new DynamoDbEventStoreOptions
		{
			EventsTableName = TableName,
			PartitionKeyAttribute = "pk",
			SortKeyAttribute = "sk",
			CreateTableIfNotExists = true,
			UseTransactionalWrite = true,
			UseOnDemandCapacity = true,
			EnableStreams = false, // LocalStack has limited streams support
		});

		return new DynamoDbEventStore(
			_dynamoDbClient,
			_streamsClient,
			options,
			NullLogger<DynamoDbEventStore>.Instance);
	}

	/// <summary>
	/// Cleans up the events table by dropping and recreating it.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		if (!IsInitialized || _dynamoDbClient == null)
		{
			return;
		}

		try
		{
			// Delete the table if it exists
			_ = await _dynamoDbClient.DeleteTableAsync(TableName).ConfigureAwait(false);

			// Wait briefly for deletion to complete
			await Task.Delay(500).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			// Table doesn't exist, that's fine
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
		_dynamoDbClient?.Dispose();
		_streamsClient?.Dispose();
		ClearRecordedActivities();

		_disposed = true;
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await _container.DisposeAsync().ConfigureAwait(false);
	}
}
