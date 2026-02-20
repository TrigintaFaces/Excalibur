// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Saga;

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
	private readonly IJsonSerializer _serializer;
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
		IJsonSerializer serializer)
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
		IJsonSerializer serializer)
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
		var docRef = _collection.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return null;
		}

		var stateJson = snapshot.GetValue<string>("stateJson");
		var result = await _serializer.DeserializeAsync<TSagaState>(stateJson).ConfigureAwait(false);

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
		var docRef = _collection.Document(docId);

		// Read existing to preserve createdUtc
		var existingSnapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		DateTimeOffset createdUtc;
		if (existingSnapshot.Exists && existingSnapshot.TryGetValue<Timestamp>("createdUtc", out var createdTimestamp))
		{
			createdUtc = createdTimestamp.ToDateTimeOffset();
		}
		else
		{
			createdUtc = now;
		}

		var data = new Dictionary<string, object>
		{
			["sagaId"] = sagaState.SagaId.ToString(),
			["sagaType"] = sagaType,
			["stateJson"] = stateJson,
			["isCompleted"] = sagaState.Completed,
			["createdUtc"] = Timestamp.FromDateTimeOffset(createdUtc),
			["updatedUtc"] = Timestamp.FromDateTimeOffset(now)
		};

		_ = await docRef.SetAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);

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
			builder.CredentialsPath = _options.CredentialsPath;
		}
		else if (!string.IsNullOrEmpty(_options.CredentialsJson))
		{
			builder.JsonCredentials = _options.CredentialsJson;
		}

		_db = await builder.BuildAsync().ConfigureAwait(false);
		_collection = _db.Collection(_options.CollectionName);
		_initialized = true;
	}
}
