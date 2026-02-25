// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Firestore.Diagnostics;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Authorization;

/// <summary>
/// Firestore implementation of <see cref="IActivityGroupGrantService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses flat collections with composite document IDs for efficient point reads:
/// {tenantId}_{userId}_{grantType}_{qualifier}
/// </para>
/// <para>
/// Uses SetAsync for upsert operations and batch operations for bulk deletes.
/// Firestore batch limit is 500 documents per batch.
/// </para>
/// </remarks>
public sealed partial class FirestoreActivityGroupGrantService : IActivityGroupGrantService, IAsyncDisposable
{
	private readonly FirestoreAuthorizationOptions _options;
	private readonly ILogger<FirestoreActivityGroupGrantService> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreActivityGroupGrantService"/> class.
	/// </summary>
	/// <param name="options">The Firestore authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreActivityGroupGrantService(
		IOptions<FirestoreAuthorizationOptions> options,
		ILogger<FirestoreActivityGroupGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreActivityGroupGrantService"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreActivityGroupGrantService(
		FirestoreDb db,
		IOptions<FirestoreAuthorizationOptions> options,
		ILogger<FirestoreActivityGroupGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_collection = db.Collection(_options.ActivityGroupsCollectionName);
		_initialized = true;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteActivityGroupGrantsByUserIdAsync(
		string userId,
		string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query for all activity groups for this user with this grant type
		var query = _collection
			.WhereEqualTo(FirestoreActivityGroupDocument.UserIdFieldName, userId)
			.WhereEqualTo(FirestoreActivityGroupDocument.GrantTypeFieldName, grantType);

		var querySnapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var docsToDelete = querySnapshot.Documents.ToList();
		if (docsToDelete.Count == 0)
		{
			return 0;
		}

		// Delete in batches of MaxBatchSize (Firestore limit is 500)
		var deletedCount = await DeleteDocumentsInBatchesAsync(docsToDelete, cancellationToken).ConfigureAwait(false);

		LogActivityGroupGrantsDeletedByUser(userId, grantType, deletedCount);
		return deletedCount;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteAllActivityGroupGrantsAsync(
		string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query for all activity groups with this grant type
		var query = _collection
			.WhereEqualTo(FirestoreActivityGroupDocument.GrantTypeFieldName, grantType);

		var querySnapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var docsToDelete = querySnapshot.Documents.ToList();
		if (docsToDelete.Count == 0)
		{
			return 0;
		}

		// Delete in batches of MaxBatchSize (Firestore limit is 500)
		var deletedCount = await DeleteDocumentsInBatchesAsync(docsToDelete, cancellationToken).ConfigureAwait(false);

		LogAllActivityGroupGrantsDeleted(grantType, deletedCount);
		return deletedCount;
	}

	/// <inheritdoc/>
	public async Task<int> InsertActivityGroupGrantAsync(
		string userId,
		string fullName,
		string? tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var docId = FirestoreActivityGroupDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var docRef = _collection.Document(docId);

		var data = FirestoreActivityGroupDocument.ToDocumentData(
			userId,
			fullName,
			tenantId,
			grantType,
			qualifier,
			expiresOn,
			grantedBy,
			now,
			now);

		// SetAsync acts as upsert - updates if exists, creates if not
		_ = await docRef.SetAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogActivityGroupGrantInserted(userId, grantType, qualifier);
		return 1;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
		string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query for all activity groups with this grant type
		var query = _collection
			.WhereEqualTo(FirestoreActivityGroupDocument.GrantTypeFieldName, grantType);

		var querySnapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var userIds = new HashSet<string>();
		foreach (var doc in querySnapshot.Documents)
		{
			_ = userIds.Add(FirestoreActivityGroupDocument.GetUserId(doc));
		}

		return userIds.ToList();
	}

	/// <summary>
	/// Initializes the Firestore client.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
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

			if (!string.IsNullOrEmpty(_options.CredentialsJson))
			{
				builder.JsonCredentials = _options.CredentialsJson;
			}
			else if (!string.IsNullOrEmpty(_options.CredentialsPath))
			{
				builder.CredentialsPath = _options.CredentialsPath;
			}

			_db = await builder.BuildAsync(cancellationToken).ConfigureAwait(false);
			_collection = _db.Collection(_options.ActivityGroupsCollectionName);

			_initialized = true;
			LogInitialized(_options.ActivityGroupsCollectionName);
		}
		finally
		{
			_ = _initLock.Release();
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
		_initLock.Dispose();
		// FirestoreDb doesn't implement IDisposable - connections are managed internally
		return ValueTask.CompletedTask;
	}

	private async Task<int> DeleteDocumentsInBatchesAsync(
		IList<DocumentSnapshot> documents,
		CancellationToken cancellationToken)
	{
		var deletedCount = 0;

		for (var i = 0; i < documents.Count; i += _options.MaxBatchSize)
		{
			var batch = _db.StartBatch();
			var batchDocs = documents.Skip(i).Take(_options.MaxBatchSize).ToList();

			foreach (var doc in batchDocs)
			{
				_ = batch.Delete(doc.Reference);
			}

			_ = await batch.CommitAsync(cancellationToken).ConfigureAwait(false);
			deletedCount += batchDocs.Count;
		}

		return deletedCount;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	[LoggerMessage(DataFirestoreEventId.ActivityGroupServiceInitialized, LogLevel.Debug,
		"Firestore activity group service initialized for collection '{CollectionName}'")]
	private partial void LogInitialized(string collectionName);

	[LoggerMessage(DataFirestoreEventId.ActivityGroupGrantInserted, LogLevel.Debug,
		"Activity group grant inserted: userId={UserId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogActivityGroupGrantInserted(string userId, string grantType, string qualifier);

	[LoggerMessage(DataFirestoreEventId.ActivityGroupGrantsDeletedByUser, LogLevel.Debug,
		"Activity group grants deleted by user: userId={UserId}, grantType={GrantType}, count={Count}")]
	private partial void LogActivityGroupGrantsDeletedByUser(string userId, string grantType, int count);

	[LoggerMessage(DataFirestoreEventId.ActivityGroupAllGrantsDeleted, LogLevel.Debug,
		"All activity group grants deleted: grantType={GrantType}, count={Count}")]
	private partial void LogAllActivityGroupGrantsDeleted(string grantType, int count);
}
