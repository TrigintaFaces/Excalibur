// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Excalibur.Data;
using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Data.Observability;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

using Google.Cloud.Firestore;

using Grpc.Core;

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
	private readonly ITenantContext? _tenantContext;
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. When supplied, the
	/// tenant becomes part of every snapshot document id.
	/// </param>
	public FirestoreSnapshotStore(
		IOptions<FirestoreSnapshotStoreOptions> options,
		ILogger<FirestoreSnapshotStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		_tenantContext = tenantContext;

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
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. When supplied, the
	/// tenant becomes part of every snapshot document id.
	/// </param>
	public FirestoreSnapshotStore(
		FirestoreDb db,
		IOptions<FirestoreSnapshotStoreOptions> options,
		ILogger<FirestoreSnapshotStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		_tenantContext = tenantContext;

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
		var docRef = _collection!.Document(documentId);

		try
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (!snapshot.Exists)
			{
				result = WriteStoreTelemetry.Results.NotFound;
				return null;
			}

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
			var snapshotResult = FromFirestoreDocument(snapshot);
#pragma warning restore IL2026
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
		var docRef = _collection!.Document(documentId);

		try
		{
			// Concurrent savers contend on ONE document, so Firestore aborts the losers with
			// "Transaction lock timeout". That is contention, not a caller error, and it must not
			// reach the caller as a raw Grpc.Core.RpcException: ISnapshotStore is a provider-neutral
			// abstraction, and every other provider reports write contention as ConcurrencyException.
			//
			// Retrying is safe BECAUSE of the version guard below. A retry re-reads inside a fresh
			// transaction, so a saver overtaken while it waited finds the higher version already
			// stored and returns through the guard instead of writing. The operation is idempotent
			// under retry, which is what makes the bounded spin correct rather than merely hopeful.
			var attempt = 0;
			while (true)
			{
				attempt++;
				try
				{
					await SaveSnapshotTransactionAsync(docRef, snapshot, r => result = r, cancellationToken)
						.ConfigureAwait(false);
					break;
				}
				catch (RpcException ex) when (IsWriteContention(ex) && attempt < MaxContendedWriteAttempts)
				{
					// Back off before re-entering the transaction; a tight respin just re-collides.
					await Task.Delay(ContendedWriteBackoff * attempt, cancellationToken).ConfigureAwait(false); // delay-ok: contention backoff between transaction attempts, not a sync-wait
				}
				catch (RpcException ex) when (IsWriteContention(ex))
				{
					// Exhausted. Report it as contention, loudly and in the abstraction's own currency
					// -- never a raw gRPC status, and never silence.
					result = WriteStoreTelemetry.Results.Failure;
					throw new ConcurrencyException(
						nameof(FirestoreSnapshotStore),
						documentId,
						snapshot.Version,
						await ReadCurrentVersionOrDefaultAsync(docRef, cancellationToken).ConfigureAwait(false));
				}
			}

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

	/// <summary>Bounded spin for a contended snapshot write. A guard against livelock, not a writer budget.</summary>
	private const int MaxContendedWriteAttempts = 8;

	/// <summary>Base backoff between contended transaction attempts; multiplied by the attempt number.</summary>
	private static readonly TimeSpan ContendedWriteBackoff = TimeSpan.FromMilliseconds(25);

	/// <summary>
	/// True when the status is Firestore reporting write contention rather than a caller error.
	/// Aborted is the documented transaction-contention status and the one observed in practice
	/// ("Transaction lock timeout"). Deliberately narrow: DeadlineExceeded is NOT treated as
	/// contention, because it also covers a genuinely unreachable backend, and retrying that
	/// would turn an infrastructure fault into a slow, silent one.
	/// </summary>
	private static bool IsWriteContention(RpcException ex) => ex.StatusCode is StatusCode.Aborted;

	/// <summary>
	/// Reads the currently stored snapshot version for diagnostics on the exhaustion path, returning -1
	/// when the document cannot be read. Never throws: it runs only while reporting another failure and
	/// must not replace that failure with its own.
	/// </summary>
	private static async Task<long> ReadCurrentVersionOrDefaultAsync(
		DocumentReference docRef,
		CancellationToken cancellationToken)
	{
		try
		{
			var doc = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
			return doc.Exists ? doc.GetValue<long>("version") : -1;
		}
		catch (RpcException)
		{
			return -1;
		}
	}

	private async Task SaveSnapshotTransactionAsync(
		DocumentReference docRef,
		ISnapshot snapshot,
		Action<string> setResult,
		CancellationToken cancellationToken)
	{
		{
			await _db!.RunTransactionAsync(async transaction =>
			{
				var existingDoc = await transaction.GetSnapshotAsync(docRef, cancellationToken).ConfigureAwait(false);

				if (existingDoc.Exists)
				{
					var existingVersion = existingDoc.GetValue<long>("version");
					if (existingVersion >= snapshot.Version)
					{
						// Older or same version - skip silently (version guard)
						setResult(WriteStoreTelemetry.Results.Conflict);
						LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version, existingVersion);
						return;
					}
				}

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
				var docData = ToFirestoreDocument(snapshot);
#pragma warning restore IL2026
				transaction.Set(docRef, docData);
			}, cancellationToken: cancellationToken).ConfigureAwait(false);
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
		var docRef = _collection!.Document(documentId);

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
		var docRef = _collection!.Document(documentId);

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
	/// Builds the document id from aggregate type and id, including the tenant when the host is multi-tenant.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <param name="aggregateId">The aggregate ID.</param>
	/// <returns>A composite document ID.</returns>
	/// <remarks>
	/// Uses "_" as the separator to match the convention this provider's grant store already follows,
	/// rather than importing the ":" shape used by the other document providers. A Firestore document id
	/// may not contain "/", so the separator choice is constrained. Single-tenant ids keep their existing
	/// shape, so documents already stored are not orphaned.
	/// </remarks>
	private string CreateDocumentId(string aggregateType, string aggregateId)
	{
		var tenantId = TenantScope.FromContext(_tenantContext).TenantId;
		return string.IsNullOrEmpty(tenantId)
			? $"{Escape(aggregateType)}_{Escape(aggregateId)}"
			: $"t_{Escape(tenantId)}_{Escape(aggregateType)}_{Escape(aggregateId)}";
	}

	// A Firestore document id may not contain '/' -- it is the path separator, so an aggregate id such as
	// "order-123/customer-456" is read as a nested collection path rather than as an id, and the snapshot is
	// written somewhere the matching read never looks. An aggregate id is caller data and may legally contain
	// any character, so it is escaped rather than rejected: every other provider accepts it.
	//
	// '%' is escaped FIRST and is what makes this reversible. Escaping only '/' would map the distinct ids
	// "a/b" and "a%2Fb" onto the same document -- a collision introduced by the escaping itself, and across
	// tenants if it landed in the tenant segment.
	private static string Escape(string value) =>
		value.Replace("%", "%25", StringComparison.Ordinal)
			.Replace("/", "%2F", StringComparison.Ordinal);

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
			["data"] = Blob.CopyFrom(snapshot.Data.ToArray())
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

#pragma warning disable CS0618 // CredentialsPath/JsonCredentials are obsolete but replacements require significant refactoring
		if (!string.IsNullOrEmpty(_options.CredentialsPath))
		{
			builder.CredentialsPath = _options.CredentialsPath;
		}
		else if (!string.IsNullOrEmpty(_options.CredentialsJson))
		{
			builder.JsonCredentials = _options.CredentialsJson;
		}
#pragma warning restore CS0618

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
