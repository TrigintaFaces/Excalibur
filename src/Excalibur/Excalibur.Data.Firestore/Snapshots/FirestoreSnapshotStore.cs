// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Excalibur.Data.Abstractions.Observability;
using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Snapshots;

/// <summary>
/// Firestore-based implementation of <see cref="ISnapshotStore"/>.
/// </summary>
/// <remarks>
/// Uses a simple collection design with documents keyed by composite ID (aggregateType_aggregateId).
/// Version ordering is enforced using Firestore transactions - older versions never overwrite newer ones.
/// </remarks>
public sealed partial class FirestoreSnapshotStore : ISnapshotStore, IAsyncDisposable, IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly FirestoreSnapshotStoreOptions _options;
	private readonly ILogger<FirestoreSnapshotStore> _logger;
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreSnapshotStore(
		IOptions<FirestoreSnapshotStoreOptions> options,
		ILogger<FirestoreSnapshotStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSnapshotStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreSnapshotStore(
		FirestoreDb db,
		IOptions<FirestoreSnapshotStoreOptions> options,
		ILogger<FirestoreSnapshotStore> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_collection = db.Collection(_options.CollectionName);
		_initialized = true;
	}

	/// <inheritdoc/>
	public async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync().ConfigureAwait(false);

		var documentId = CreateDocumentId(aggregateType, aggregateId);
		var docRef = _collection.Document(documentId);

		try
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (!snapshot.Exists)
			{
				result = WriteStoreTelemetry.Results.NotFound;
				return null;
			}

			var snapshotResult = FromFirestoreDocument(snapshot);
			LogSnapshotRetrieved(aggregateType, aggregateId, snapshotResult.Version);
			return snapshotResult;
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.AggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.AggregateType);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync().ConfigureAwait(false);

		var documentId = CreateDocumentId(snapshot.AggregateType, snapshot.AggregateId);
		var docRef = _collection.Document(documentId);

		try
		{
			// Use RunTransactionAsync for atomic read-check-write to enforce version ordering
			await _db.RunTransactionAsync(async transaction =>
			{
				var existingDoc = await transaction.GetSnapshotAsync(docRef, cancellationToken).ConfigureAwait(false);

				if (existingDoc.Exists)
				{
					var existingVersion = existingDoc.GetValue<long>("version");
					if (existingVersion >= snapshot.Version)
					{
						// Older or same version - skip silently (version guard)
						result = WriteStoreTelemetry.Results.Conflict;
						LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version, existingVersion);
						return;
					}
				}

				var docData = ToFirestoreDocument(snapshot);
				transaction.Set(docRef, docData);
			}, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Firestore,
				"save",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync().ConfigureAwait(false);

		var documentId = CreateDocumentId(aggregateType, aggregateId);
		var docRef = _collection.Document(documentId);

		try
		{
			_ = await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			LogSnapshotDeleted(aggregateType, aggregateId);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Firestore,
				"delete",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync().ConfigureAwait(false);

		// For Firestore's simple one-snapshot-per-aggregate model, we only delete
		// if the current snapshot's version is older than the specified version
		var documentId = CreateDocumentId(aggregateType, aggregateId);
		var docRef = _collection.Document(documentId);

		try
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (!snapshot.Exists)
			{
				result = WriteStoreTelemetry.Results.NotFound;
				return;
			}

			var currentVersion = snapshot.GetValue<long>("version");
			if (currentVersion < olderThanVersion)
			{
				_ = await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
				LogSnapshotsDeletedOlderThan(1, olderThanVersion, aggregateType, aggregateId);
			}
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Firestore,
				"delete_older_than",
				result,
				stopwatch.Elapsed);
		}
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

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		// FirestoreDb doesn't implement IDisposable - connections are managed internally
	}

	/// <summary>
	/// Creates a document ID from aggregate type and ID.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <param name="aggregateId">The aggregate ID.</param>
	/// <returns>A composite document ID.</returns>
	private static string CreateDocumentId(string aggregateType, string aggregateId)
		=> $"{aggregateType}_{aggregateId}";

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static Dictionary<string, object> ToFirestoreDocument(ISnapshot snapshot)
	{
		var doc = new Dictionary<string, object>
		{
			["snapshotId"] = snapshot.SnapshotId,
			["aggregateId"] = snapshot.AggregateId,
			["aggregateType"] = snapshot.AggregateType,
			["version"] = snapshot.Version,
			["createdAt"] = snapshot.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
			["data"] = Blob.CopyFrom(snapshot.Data)
		};

		// Serialize metadata as JSON string if present
		if (snapshot.Metadata is { Count: > 0 })
		{
			doc["metadata"] = JsonSerializer.Serialize(snapshot.Metadata, JsonOptions);
		}

		return doc;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static ISnapshot FromFirestoreDocument(DocumentSnapshot doc)
	{
		Blob? dataBlob = doc.TryGetValue<Blob>("data", out var blob) ? blob : null;

		IDictionary<string, object>? metadata = null;
		if (doc.TryGetValue<string>("metadata", out var metadataJson) && !string.IsNullOrEmpty(metadataJson))
		{
			metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson, JsonOptions);
		}

		return new Snapshot
		{
			SnapshotId = doc.GetValue<string>("snapshotId"),
			AggregateId = doc.GetValue<string>("aggregateId"),
			AggregateType = doc.GetValue<string>("aggregateType"),
			Version = doc.GetValue<long>("version"),
			CreatedAt = DateTimeOffset.Parse(doc.GetValue<string>("createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
			Data = dataBlob?.ByteString.ToByteArray() ?? [],
			Metadata = metadata
		};
	}

	private async Task EnsureInitializedAsync()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

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

	// Logging methods using LoggerMessage source generator
	[LoggerMessage(DataFirestoreEventId.SnapshotSaved, LogLevel.Debug,
		"Saved snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotSaved(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataFirestoreEventId.SnapshotVersionSkipped, LogLevel.Debug,
		"Skipped older snapshot for {AggregateType}/{AggregateId} at version {Version} (existing version: {ExistingVersion})")]
	private partial void LogSnapshotVersionSkipped(string aggregateType, string aggregateId, long version, long existingVersion);

	[LoggerMessage(DataFirestoreEventId.SnapshotRetrieved, LogLevel.Debug,
		"Retrieved snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotRetrieved(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataFirestoreEventId.SnapshotDeleted, LogLevel.Debug, "Deleted snapshot for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotDeleted(string aggregateType, string aggregateId);

	[LoggerMessage(DataFirestoreEventId.SnapshotsDeletedOlderThan, LogLevel.Information,
		"Deleted {Count} snapshots older than version {Version} for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotsDeletedOlderThan(int count, long version, string aggregateType, string aggregateId);
}
