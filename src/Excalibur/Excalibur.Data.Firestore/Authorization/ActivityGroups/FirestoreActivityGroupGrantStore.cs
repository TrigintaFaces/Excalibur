// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Grpc.Core;
using Excalibur.A3.Authorization;
using Excalibur.Data.Firestore.Diagnostics;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Authorization;

/// <summary>
/// Firestore implementation of <see cref="IActivityGroupGrantStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses flat collections with composite document IDs for efficient point reads, composed by
/// <see cref="FirestoreActivityGroupDocument.CreateDocumentId"/> so distinct terms can never alias one
/// document. A document composed under this store's pre-injective raw join
/// (<c>{tenantId}_{userId}_{grantType}_{qualifier}</c>, an id shape no longer written) is unaddressable
/// by id; <see cref="EnsureLegacyActivityGroupDocumentsAreAbsentAsync"/> refuses an insert that would
/// silently write a second document beside it rather than updating it.
/// </para>
/// <para>
/// Uses SetAsync for upsert operations and batch operations for bulk deletes.
/// Firestore batch limit is 500 documents per batch.
/// </para>
/// </remarks>
public sealed partial class FirestoreActivityGroupGrantStore : IActivityGroupGrantStore, IAsyncDisposable
{
	private readonly FirestoreAuthorizationOptions _options;
	private readonly ILogger<FirestoreActivityGroupGrantStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Lower bound of the document-id range every id
	/// <see cref="FirestoreActivityGroupDocument.CreateDocumentId"/> produces.
	/// </summary>
	private const string CurrentIdPrefix = $"{FirestoreActivityGroupDocument.IdPrefix}_";

	/// <summary>
	/// Exclusive upper bound of the current-id-shape range. "_" (0x5F) is the highest character the prefix
	/// can end in, so the next byte value, "`" (0x60), bounds every id sharing the prefix and none that do
	/// not -- the same technique <see cref="FirestoreGrantStore"/> uses for grant document ids.
	/// </summary>
	private const string CurrentIdPrefixUpperBound = $"{FirestoreActivityGroupDocument.IdPrefix}`";

	// See FirestoreGrantStore._legacyDocumentsProbed.
	private volatile bool _legacyDocumentsProbed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreActivityGroupGrantStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreActivityGroupGrantStore(
		IOptions<FirestoreAuthorizationOptions> options,
		ILogger<FirestoreActivityGroupGrantStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreActivityGroupGrantStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreActivityGroupGrantStore(
		FirestoreDb db,
		IOptions<FirestoreAuthorizationOptions> options,
		ILogger<FirestoreActivityGroupGrantStore> logger)
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
		var query = _collection!
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
		var query = _collection!
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
		string tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
		await EnsureLegacyActivityGroupDocumentsAreAbsentAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var docId = FirestoreActivityGroupDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var docRef = _collection!.Document(docId);

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
		var query = _collection!
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
				// Point this client at the emulator directly. The process-wide FIRESTORE_EMULATOR_HOST
				// variable is first-write-wins, so routing through it lets a second store silently talk to
				// another store's emulator. Endpoint and EmulatorDetection.EmulatorOnly are mutually
				// exclusive -- setting both throws -- so an explicit endpoint with insecure credentials is
				// the combination that reaches an emulator per instance.
				builder.Endpoint = _options.EmulatorHost;
				builder.ChannelCredentials = ChannelCredentials.Insecure;
			}

#pragma warning disable CS0618 // CredentialsPath/JsonCredentials are obsolete but replacements require significant refactoring
			if (!string.IsNullOrEmpty(_options.CredentialsJson))
			{
				builder.JsonCredentials = _options.CredentialsJson;
			}
			else if (!string.IsNullOrEmpty(_options.CredentialsPath))
			{
				builder.CredentialsPath = _options.CredentialsPath;
			}
#pragma warning restore CS0618

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
		_initLock?.Dispose();
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
			var batch = _db!.StartBatch();
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

	/// <summary>
	/// Verifies, at most once per store instance, that an insert cannot silently produce a second document
	/// beside one composed under the pre-injective id shape (see <see cref="FirestoreGrantStore"/>'s
	/// analogous guard, which this mirrors).
	/// </summary>
	/// <remarks>
	/// Called only from <see cref="InsertActivityGroupGrantAsync"/>, the only method here that addresses a
	/// document by id. Every other method here (the two delete methods and the distinct-user-ids query)
	/// filters on stored FIELD values, which a legacy document carries identically to a current one, so
	/// they find and act on legacy documents correctly without this guard.
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task EnsureLegacyActivityGroupDocumentsAreAbsentAsync(CancellationToken cancellationToken)
	{
		if (_legacyDocumentsProbed)
		{
			return;
		}

		await RefuseLegacyActivityGroupDocumentsAsync(cancellationToken).ConfigureAwait(false);
		_legacyDocumentsProbed = true;
	}

	/// <summary>
	/// Refuses when the activity-groups collection still holds a document composed under the pre-injective
	/// id shape (<c>{tenantId}_{userId}_{grantType}_{qualifier}</c>, joined raw with no prefix and no
	/// escaping). Called only through <see cref="EnsureLegacyActivityGroupDocumentsAreAbsentAsync"/>.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The collection holds at least one activity-group grant document composed under the legacy id shape.
	/// </exception>
	private async Task RefuseLegacyActivityGroupDocumentsAsync(CancellationToken cancellationToken)
	{
		Query[] probes =
		[
			_collection!.WhereLessThan(FieldPath.DocumentId, CurrentIdPrefix).Limit(1),
			_collection!.WhereGreaterThanOrEqualTo(FieldPath.DocumentId, CurrentIdPrefixUpperBound).Limit(1)
		];

		foreach (var probe in probes)
		{
			var snapshot = await probe.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (snapshot.Count == 0)
			{
				continue;
			}

			var legacyDocumentId = snapshot.Documents[0].Id;

			throw new InvalidOperationException(
				$"Activity-groups collection '{_options.ActivityGroupsCollectionName}' holds at least one " +
				$"document whose identifier ('{legacyDocumentId}') was composed under the legacy shape " +
				"'{tenantId}_{userId}_{grantType}_{qualifier}', joined without a prefix or escaping. " +
				"Inserting the same activity-group grant now would write a second document beside it " +
				"rather than updating it. Nothing has been modified. The tenant, user, grant type and " +
				"qualifier are recorded on the document's own fields, so re-key each legacy document to " +
				$"'{FirestoreActivityGroupDocument.IdPrefix}_<tenantId>_<userId>_<grantType>_<qualifier>' " +
				"(escaping '%', '/' and '_' within each term as '%25', '%2F' and '%5F') using its own " +
				"field values, delete the legacy document, and start the application again.");
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
