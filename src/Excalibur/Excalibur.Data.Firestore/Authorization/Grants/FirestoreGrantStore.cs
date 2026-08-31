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
/// Firestore implementation of <see cref="IGrantStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses flat collections with composite document IDs for efficient point reads, composed by
/// <see cref="FirestoreGrantDocument.CreateDocumentId"/> so distinct terms can never alias one document
/// (see its remarks). A document composed under this store's pre-injective raw join
/// (<c>{tenantId}_{userId}_{grantType}_{qualifier}</c>, an id shape no longer written) is unaddressable
/// under the current composition; <see cref="EnsureLegacyGrantDocumentsAreAbsentAsync"/> refuses rather
/// than silently treating it as an absent grant.
/// </para>
/// <para>
/// Uses SetAsync for upsert operations and UpdateAsync for soft deletes.
/// </para>
/// </remarks>
public sealed partial class FirestoreGrantStore : IGrantStore, IDurableGrantStore, IGrantQueryStore, IAsyncDisposable
{
	private readonly FirestoreAuthorizationOptions _options;
	private readonly ILogger<FirestoreGrantStore> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Lower bound of the document-id range every id <see cref="FirestoreGrantDocument.CreateDocumentId"/>
	/// produces. Declared once and consumed only by the legacy-document probe, so the shape the store
	/// writes and the shape it refuses to read cannot drift apart.
	/// </summary>
	private const string CurrentIdPrefix = $"{FirestoreGrantDocument.IdPrefix}_";

	/// <summary>
	/// Exclusive upper bound of the current-id-shape range, used by the legacy-document probe. "_" (0x5F)
	/// is the highest character the prefix can end in, so the next byte value, "`" (0x60), bounds every
	/// id sharing the prefix and none that do not.
	/// </summary>
	private const string CurrentIdPrefixUpperBound = $"{FirestoreGrantDocument.IdPrefix}`";

	// Set only once the legacy-document probe has come back clean, mirroring the saga store's
	// _legacyDocumentsProbed: unsynchronised (a duplicate probe costs two extra range reads, not
	// correctness), and set only on a clean result so a collection holding a legacy document keeps
	// refusing every call rather than only the first.
	private volatile bool _legacyDocumentsProbed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreGrantStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">Time source used to evaluate grant expiry. Defaults to <see cref="System.TimeProvider.System"/> when not supplied.</param>
	public FirestoreGrantStore(
		IOptions<FirestoreAuthorizationOptions> options,
		ILogger<FirestoreGrantStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreGrantStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">Time source used to evaluate grant expiry. Defaults to <see cref="System.TimeProvider.System"/> when not supplied.</param>
	public FirestoreGrantStore(
		FirestoreDb db,
		IOptions<FirestoreAuthorizationOptions> options,
		ILogger<FirestoreGrantStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_collection = db.Collection(_options.GrantsCollectionName);
		_initialized = true;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		string? revokedBy,
		DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
		await EnsureLegacyGrantDocumentsAreAbsentAsync(cancellationToken).ConfigureAwait(false);

		var docId = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var docRef = _collection!.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
		if (!snapshot.Exists)
		{
			return 0;
		}

		if (revokedBy is not null && revokedOn.HasValue)
		{
			// Soft delete by marking as revoked
			var updateData = FirestoreGrantDocument.CreateRevokeUpdate(revokedBy, revokedOn.Value);
			_ = await docRef.UpdateAsync(updateData, cancellationToken: cancellationToken).ConfigureAwait(false);
			LogGrantRevoked(userId, tenantId, grantType, qualifier);
		}
		else
		{
			// Hard delete
			_ = await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			LogGrantDeleted(userId, tenantId, grantType, qualifier);
		}

		return 1;
	}

	/// <inheritdoc/>
	public async Task<bool> GrantExistsAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
		await EnsureLegacyGrantDocumentsAreAbsentAsync(cancellationToken).ConfigureAwait(false);

		var docId = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var docRef = _collection!.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return false;
		}

		// Check if revoked
		if (snapshot.TryGetValue<bool>(FirestoreGrantDocument.IsRevokedFieldName, out var isRevoked) && isRevoked)
		{
			return false;
		}

		return true;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(
		string? userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var query = _collection!
			.WhereEqualTo(FirestoreGrantDocument.TenantIdFieldName, tenantId)
			.WhereEqualTo(FirestoreGrantDocument.GrantTypeFieldName, grantType)
			.WhereEqualTo(FirestoreGrantDocument.QualifierFieldName, qualifier)
			.WhereEqualTo(FirestoreGrantDocument.IsRevokedFieldName, false);

		if (userId is not null)
		{
			query = query.WhereEqualTo(FirestoreGrantDocument.UserIdFieldName, userId);
		}

		var querySnapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var results = new List<Grant>();
		foreach (var doc in querySnapshot.Documents)
		{
			var grant = FirestoreGrantDocument.FromSnapshot(doc);
			if (grant is not null)
			{
				results.Add(grant);
			}
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<Grant?> GetGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
		await EnsureLegacyGrantDocumentsAreAbsentAsync(cancellationToken).ConfigureAwait(false);

		var docId = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var docRef = _collection!.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		return FirestoreGrantDocument.FromSnapshot(snapshot);
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
		GetAllGrantsAsync(userId, includeExpired: false, cancellationToken);

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, bool includeExpired,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query by user ID across all tenants
		var query = _collection!
			.WhereEqualTo(FirestoreGrantDocument.UserIdFieldName, userId)
			.WhereEqualTo(FirestoreGrantDocument.IsRevokedFieldName, false);

		var querySnapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		// Default-secure: exclude expired grants unless explicitly requested.
		var now = _timeProvider.GetUtcNow();
		var results = new List<Grant>();
		foreach (var doc in querySnapshot.Documents)
		{
			var grant = FirestoreGrantDocument.FromSnapshot(doc);
			if (grant is not null && (includeExpired || grant.IsActive(now)))
			{
				results.Add(grant);
			}
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(grant);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
		await EnsureLegacyGrantDocumentsAreAbsentAsync(cancellationToken).ConfigureAwait(false);

		var docId = FirestoreGrantDocument.CreateDocumentId(grant.TenantId, grant.UserId, grant.GrantType, grant.Qualifier);
		var docRef = _collection!.Document(docId);
		var data = FirestoreGrantDocument.ToDocumentData(grant);

		// SetAsync with merge behavior acts as upsert
		_ = await docRef.SetAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogGrantSaved(grant.UserId, grant.TenantId, grant.GrantType, grant.Qualifier);
		return 1;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query by user ID across all tenants
		var query = _collection!
			.WhereEqualTo(FirestoreGrantDocument.UserIdFieldName, userId)
			.WhereEqualTo(FirestoreGrantDocument.IsRevokedFieldName, false);

		var querySnapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var result = new Dictionary<string, object>();
		foreach (var doc in querySnapshot.Documents)
		{
			var grant = FirestoreGrantDocument.FromSnapshot(doc);
			if (grant is not null)
			{
				var key = $"{grant.TenantId}:{grant.GrantType}:{grant.Qualifier}";
				result[key] = grant;
			}
		}

		return result;
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
			_collection = _db.Collection(_options.GrantsCollectionName);

			_initialized = true;
			LogInitialized(_options.GrantsCollectionName);
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IDurableGrantStore))
		{
			return this;
		}

		if (serviceType == typeof(IGrantQueryStore))
		{
			return this;
		}

		return null;
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

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies, at most once per store instance, that a grant this store cannot find is genuinely absent
	/// rather than merely unaddressable under an id shape from before <c>FirestoreDocumentId.Compose</c>
	/// made the composition injective.
	/// </summary>
	/// <remarks>
	/// Called from every point at which this store is about to act on the ABSENCE of a grant document, or
	/// write one by id, and from nowhere else -- the same discipline the Firestore saga store's
	/// EnsureEmptyReadIsTrustworthyAsync uses for saga documents. The
	/// query-based methods (<see cref="GetMatchingGrantsAsync"/>, <see cref="GetAllGrantsAsync(string, CancellationToken)"/>,
	/// <see cref="FindUserGrantsAsync"/>) filter on stored FIELD values, which a legacy document carries
	/// identically to a current one, so they need no guard.
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task EnsureLegacyGrantDocumentsAreAbsentAsync(CancellationToken cancellationToken)
	{
		if (_legacyDocumentsProbed)
		{
			return;
		}

		await RefuseLegacyGrantDocumentsAsync(cancellationToken).ConfigureAwait(false);
		_legacyDocumentsProbed = true;
	}

	/// <summary>
	/// Refuses when the grants collection still holds a document composed under the pre-injective id shape
	/// (<c>{tenantId}_{userId}_{grantType}_{qualifier}</c>, joined raw with no prefix and no escaping).
	/// Called only through <see cref="EnsureLegacyGrantDocumentsAreAbsentAsync"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Such a document is unaddressable under the current key shape, and the failure is silent and worse
	/// than an error: a point read for the grant finds nothing, which reads as the subject never having
	/// been granted the permission rather than as a lookup fault. Depending on how the caller's policy
	/// composes, that is either a lockout or a silently narrowed permission set -- neither surfaces as a
	/// fault. On the save path the same silence writes a second document beside the first rather than
	/// updating it, which is the grant-store analogue of the saga store's "second, duplicate saga written
	/// beside the original."
	/// </para>
	/// <para>
	/// Nothing is modified. Which tenant a legacy document belongs to is recorded in the document itself
	/// (the tenant_id field), so -- unlike the saga store's key, which carries no tenant term to recover
	/// from -- a legacy grant document is always re-keyable without external input; the message states the
	/// procedure.
	/// </para>
	/// <para>
	/// Every id this store composes starts with <see cref="CurrentIdPrefix"/>, so a legacy id sorts
	/// either below it or at/above <see cref="CurrentIdPrefixUpperBound"/> -- the same two-probe range
	/// technique the Firestore saga store's RefuseLegacyUntenantedDocumentsAsync uses, each bounded
	/// to a single document.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The collection holds at least one grant document composed under the legacy id shape.
	/// </exception>
	private async Task RefuseLegacyGrantDocumentsAsync(CancellationToken cancellationToken)
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
				$"Grants collection '{_options.GrantsCollectionName}' holds at least one grant document " +
				$"whose identifier ('{legacyDocumentId}') was composed under the legacy shape " +
				"'{tenantId}_{userId}_{grantType}_{qualifier}', joined without a prefix or escaping. " +
				"Those documents are unaddressable under the current key shape: a point read for the " +
				"grant reports no grant, which the caller reads as an absent permission rather than a " +
				"lookup fault, and a save writes a second document beside the first rather than updating " +
				"it. Nothing has been modified. The tenant, user, grant type and qualifier are recorded " +
				"on the document's own fields, so re-key each legacy document to " +
				$"'{FirestoreGrantDocument.IdPrefix}_<tenantId>_<userId>_<grantType>_<qualifier>' " +
				"(escaping '%', '/' and '_' within each term as '%25', '%2F' and '%5F') using its own " +
				"field values, delete the legacy document, and start the application again.");
		}
	}

	[LoggerMessage(DataFirestoreEventId.GrantServiceInitialized, LogLevel.Debug,
		"Firestore grant service initialized for collection '{CollectionName}'")]
	private partial void LogInitialized(string collectionName);

	[LoggerMessage(DataFirestoreEventId.GrantSaved, LogLevel.Debug,
		"Grant saved: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantSaved(string userId, string tenantId, string grantType, string qualifier);

	[LoggerMessage(DataFirestoreEventId.GrantDeleted, LogLevel.Debug,
		"Grant deleted: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantDeleted(string userId, string tenantId, string grantType, string qualifier);

	[LoggerMessage(DataFirestoreEventId.GrantRevoked, LogLevel.Debug,
		"Grant revoked: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantRevoked(string userId, string tenantId, string grantType, string qualifier);
}
