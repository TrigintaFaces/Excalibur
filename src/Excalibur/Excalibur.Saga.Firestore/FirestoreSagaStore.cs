// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL2046, IL3050, IL3051 // AOT: Cloud-native provider uses reflection-based serialization
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data;
using Excalibur.Data.Firestore;
using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Firestore;

/// <summary>
/// Firestore implementation of <see cref="ISagaStore"/> using Firestore documents per saga instance.
/// </summary>
/// <remarks>
/// <para>
/// Uses Firestore transactions for optimistic concurrency. Each saga instance is stored
/// as a document keyed by "{sagaId}_{sagaType}".
/// </para>
/// <para>
/// Uses read-then-set pattern to preserve the createdUtc timestamp on updates.
/// </para>
/// </remarks>
public sealed partial class FirestoreSagaStore : ISagaStore, IAsyncDisposable
{
	private readonly FirestoreSagaOptions _options;
	private readonly ILogger<FirestoreSagaStore> _logger;
	private readonly DispatchJsonSerializer _serializer;
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSagaStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore saga options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	public FirestoreSagaStore(
		IOptions<FirestoreSagaOptions> options,
		ILogger<FirestoreSagaStore> logger,
		DispatchJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSagaStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore saga options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	public FirestoreSagaStore(
		FirestoreDb db,
		IOptions<FirestoreSagaOptions> options,
		ILogger<FirestoreSagaStore> logger,
		DispatchJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		_collection = db.Collection(_options.CollectionName);
		_initialized = true;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docId = GetDocumentId(sagaId, typeof(TSagaState).Name);
		var docRef = _collection!.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return null;
		}

		var stateJson = snapshot.GetValue<string>("stateJson");
		var result = _serializer.Deserialize<TSagaState>(stateJson);
		if (result is not null)
		{
			// The authoritative optimistic-concurrency version is the dedicated "version" field, NOT the
			// version embedded in stateJson (serialized before the store-owns-increment write-back, so it
			// carries the stale pre-save version). Apply it so load-modify-save gates against the real value.
			result.Version = snapshot.TryGetValue<long>("version", out var persistedVersion) ? persistedVersion : 0L;
		}

		LogSagaLoaded(typeof(TSagaState).Name, sagaId);
		return result;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var stateJson = _serializer.Serialize(sagaState);
		var sagaType = typeof(TSagaState).Name;
		var docId = GetDocumentId(sagaState.SagaId, sagaType);
		var docRef = _collection!.Document(docId);
		var expectedVersion = sagaState.Version;

		// Optimistic concurrency (bd-e1tsq2), honoring this store's documented "uses transactions for
		// optimistic concurrency" contract (previously a non-transactional read-then-SetAsync that lost
		// concurrent writes). The read of the current version and the version-gated write happen inside one
		// Firestore transaction (RunTransactionAsync auto-retries on contention, re-reading on each attempt),
		// mirroring SqlServerSagaStore's version-gated MERGE: the write only proceeds when the persisted
		// version equals the loaded (expected) version, otherwise a ConcurrencyException is thrown.
		await _db!.RunTransactionAsync(
			async transaction =>
			{
				var existingSnapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken).ConfigureAwait(false);

				var currentVersion = 0L;
				var createdUtc = now;
				if (existingSnapshot.Exists)
				{
					if (existingSnapshot.TryGetValue<long>("version", out var persistedVersion))
					{
						currentVersion = persistedVersion;
					}

					if (existingSnapshot.TryGetValue<Timestamp>("createdUtc", out var createdTimestamp))
					{
						createdUtc = createdTimestamp.ToDateTimeOffset();
					}
				}

				if (currentVersion != expectedVersion)
				{
					throw new ConcurrencyException(
						nameof(SagaState),
						sagaState.SagaId.ToString(),
						expectedVersion,
						currentVersion);
				}

				var data = new Dictionary<string, object>
				{
					["sagaId"] = sagaState.SagaId.ToString(),
					["sagaType"] = sagaType,
					["stateJson"] = stateJson,
					["isCompleted"] = sagaState.Completed,
					["version"] = expectedVersion + 1,
					["createdUtc"] = Timestamp.FromDateTimeOffset(createdUtc),
					["updatedUtc"] = Timestamp.FromDateTimeOffset(now)
				};

				transaction.Set(docRef, data);
			},
			options: null,
			cancellationToken).ConfigureAwait(false);

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = expectedVersion + 1;

		LogSagaSaved(sagaType, sagaState.SagaId, sagaState.Completed);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		// FirestoreDb doesn't implement IDisposable - connections are managed internally
		return ValueTask.CompletedTask;
	}

	private static string GetDocumentId(Guid sagaId, string sagaType) =>
		$"{sagaId}_{sagaType}";

	[LoggerMessage(DataFirestoreEventId.SagaLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataFirestoreEventId.SagaSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]
	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);

	private async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		var builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId };

		if (!string.IsNullOrEmpty(_options.EmulatorHost))
		{
			builder.EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly;
			_ = FirestoreEmulatorHelper.TryConfigureEmulatorHost(_options.EmulatorHost);
		}

		if (!string.IsNullOrEmpty(_options.CredentialsPath))
		{
#pragma warning disable CS0618 // Obsolete CredentialsPath/JsonCredentials
			builder.CredentialsPath = _options.CredentialsPath;
#pragma warning restore CS0618
		}
		else if (!string.IsNullOrEmpty(_options.CredentialsJson))
		{
#pragma warning disable CS0618
			builder.JsonCredentials = _options.CredentialsJson;
#pragma warning restore CS0618
		}

		_db = await builder.BuildAsync().ConfigureAwait(false);
		_collection = _db.Collection(_options.CollectionName);
		_initialized = true;
	}
}
