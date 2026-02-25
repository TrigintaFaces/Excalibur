// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MsOptions = Microsoft.Extensions.Options.Options;
using Excalibur.Data.Postgres.EventSourcing;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Testcontainers.PostgreSql;

#pragma warning disable CA2100 // SQL strings are safe - table name is a constant in test fixture

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Test fixture for Postgres EventStore telemetry integration testing.
/// Provides a Postgres container with ActivitySource listeners for capturing telemetry data during tests.
/// </summary>
public sealed class PostgresEventStoreTelemetryTestFixture : IAsyncLifetime, IDisposable
{
	private readonly PostgreSqlContainer _container;
	private readonly ActivityListener _activityListener;
	private readonly List<Activity> _recordedActivities = [];
	private readonly DateTimeOffset _creationTime = DateTimeOffset.UtcNow;
	private bool _initialized;
	private bool _disposed;

	/// <summary>
	/// Gets the connection string for the Postgres container.
	/// </summary>
	public string ConnectionString => _container.GetConnectionString();

	/// <summary>
	/// Gets the table name for events.
	/// </summary>
	public string TableName { get; } = "event_store_events";

	/// <summary>
	/// Gets the schema name for events.
	/// </summary>
	public string SchemaName { get; } = "public";

	/// <summary>
	/// Static constructor to enable Npgsql legacy timestamp behavior.
	/// </summary>
	static PostgresEventStoreTelemetryTestFixture()
	{
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStoreTelemetryTestFixture"/> class.
	/// </summary>
	public PostgresEventStoreTelemetryTestFixture()
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithName($"postgres-telemetry-test-{Guid.NewGuid():N}")
			.WithDatabase("eventstore_telemetry_test")
			.WithUsername("postgres")
			.WithPassword("postgres_password")
			.WithCleanUp(true)
			.Build();

		// Set up activity listener for capturing spans
		_activityListener = new ActivityListener
		{
			ShouldListenTo = source =>
				source.Name == EventSourcingActivitySource.Name,

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

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);
			await EnsureSchemaInitializedAsync().ConfigureAwait(false);
			IsInitialized = true;
		}
		catch (Exception)
		{
			// Container may fail to start if Docker is not available
			IsInitialized = false;
		}
	}

	/// <summary>
	/// Ensures the event store schema is initialized.
	/// </summary>
	private async Task EnsureSchemaInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var createTableSql = $"""
			CREATE TABLE IF NOT EXISTS {SchemaName}.{TableName} (
				global_sequence BIGSERIAL PRIMARY KEY,
				event_id VARCHAR(255) NOT NULL UNIQUE,
				aggregate_id VARCHAR(255) NOT NULL,
				aggregate_type VARCHAR(255) NOT NULL,
				event_type VARCHAR(255) NOT NULL,
				event_data BYTEA NOT NULL,
				metadata BYTEA,
				version BIGINT NOT NULL,
				timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
				is_dispatched BOOLEAN NOT NULL DEFAULT FALSE,
				UNIQUE (aggregate_id, aggregate_type, version)
			);

			CREATE INDEX IF NOT EXISTS idx_events_aggregate
				ON {SchemaName}.{TableName}(aggregate_id, aggregate_type, version);

			CREATE INDEX IF NOT EXISTS idx_events_undispatched
				ON {SchemaName}.{TableName}(is_dispatched, global_sequence) WHERE is_dispatched = false;

			CREATE INDEX IF NOT EXISTS idx_events_type
				ON {SchemaName}.{TableName}(event_type);
			""";

		await using var command = new NpgsqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

		_initialized = true;
	}

	/// <summary>
	/// Creates a new NpgsqlConnection to the container.
	/// </summary>
	public NpgsqlConnection CreateConnection() => new(ConnectionString);

	/// <summary>
	/// Creates a PostgresEventStore configured for this container.
	/// </summary>
	public PostgresEventStore CreateEventStore()
	{
		if (!IsInitialized)
		{
			throw new InvalidOperationException("Test fixture not initialized. Postgres container may not be available.");
		}

		var options = MsOptions.Create(new PostgresEventStoreOptions
		{
			SchemaName = SchemaName,
			EventsTableName = TableName,
		});

		return new PostgresEventStore(
			() => CreateConnection(),
			options,
			NullLogger<PostgresEventStore>.Instance);
	}

	/// <summary>
	/// Cleans up all items from the events table.
	/// </summary>
	public async Task CleanupTableAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var truncateSql = $"TRUNCATE TABLE {SchemaName}.{TableName} RESTART IDENTITY";
		await using var command = new NpgsqlCommand(truncateSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
