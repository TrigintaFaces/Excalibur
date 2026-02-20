// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MsOptions = Microsoft.Extensions.Options.Options;
using Excalibur.EventSourcing.Firestore;
using Excalibur.EventSourcing.Observability;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Testcontainers.Firestore;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Test fixture for Firestore EventStore telemetry integration testing.
/// Provides a Firestore emulator container and ActivitySource listeners
/// for capturing telemetry data during tests.
/// </summary>
/// <remarks>
/// Note: The Firestore emulator requires Docker.
/// Tests may fail if Docker is not available.
/// Firestore uses RpcException with StatusCode.AlreadyExists for concurrency conflicts.
/// </remarks>
public sealed class FirestoreEventStoreTelemetryTestFixture : IAsyncLifetime, IDisposable
{
	private readonly FirestoreContainer _container;
	private readonly ActivityListener _activityListener;
	private readonly List<Activity> _recordedActivities = [];
	private readonly DateTimeOffset _creationTime = DateTimeOffset.UtcNow;
	private bool _disposed;

	private FirestoreDb? _firestoreDb;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreEventStoreTelemetryTestFixture"/> class.
	/// </summary>
	public FirestoreEventStoreTelemetryTestFixture()
	{
		_container = new FirestoreBuilder()
			.WithImage("gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators")
			.WithName($"firestore-telemetry-test-{Guid.NewGuid():N}")
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
	/// Gets the collection name for events.
	/// </summary>
	public string CollectionName { get; } = "events";

	/// <summary>
	/// Gets the project ID used for the emulator.
	/// </summary>
	public string ProjectId { get; } = "test-project";

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);

			// Use FirestoreDbBuilder with explicit endpoint and insecure credentials
			// This is required for emulator connections - environment variables don't work reliably
			var firestoreDbBuilder = new FirestoreDbBuilder
			{
				ProjectId = ProjectId,
				Endpoint = _container.GetEmulatorEndpoint(),
				ChannelCredentials = ChannelCredentials.Insecure,
			};
			_firestoreDb = await firestoreDbBuilder.BuildAsync().ConfigureAwait(false);

			IsInitialized = true;
		}
		catch (Exception)
		{
			// Container may fail to start in CI environments
			IsInitialized = false;
		}
	}

	/// <summary>
	/// Creates a FirestoreEventStore configured for this container.
	/// </summary>
	public FirestoreEventStore CreateEventStore()
	{
		if (!IsInitialized || _firestoreDb == null)
		{
			throw new InvalidOperationException("Test fixture not initialized. Firestore emulator may not be available.");
		}

		var options = MsOptions.Create(new FirestoreEventStoreOptions
		{
			ProjectId = ProjectId,
			EventsCollectionName = CollectionName,
			EmulatorHost = _container.GetEmulatorEndpoint(),
			CreateCollectionIfNotExists = true,
		});

		return new FirestoreEventStore(
			_firestoreDb,
			options,
			NullLogger<FirestoreEventStore>.Instance);
	}

	/// <summary>
	/// Cleans up the events collection by deleting all documents.
	/// </summary>
	public async Task CleanupCollectionAsync()
	{
		if (!IsInitialized || _firestoreDb == null)
		{
			return;
		}

		try
		{
			var collectionRef = _firestoreDb.Collection(CollectionName);
			var snapshot = await collectionRef.Limit(500).GetSnapshotAsync().ConfigureAwait(false);

			// Delete documents in batches
			while (snapshot.Count > 0)
			{
				var batch = _firestoreDb.StartBatch();
				foreach (var doc in snapshot.Documents)
				{
					_ = batch.Delete(doc.Reference);
				}

				_ = await batch.CommitAsync().ConfigureAwait(false);
				snapshot = await collectionRef.Limit(500).GetSnapshotAsync().ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Collection might not exist, that's fine
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
		ClearRecordedActivities();

		_disposed = true;
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await _container.DisposeAsync().ConfigureAwait(false);
	}
}
