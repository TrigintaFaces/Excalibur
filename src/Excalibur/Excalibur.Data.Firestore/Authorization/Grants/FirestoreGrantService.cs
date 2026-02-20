// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Firestore.Diagnostics;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Authorization;

/// <summary>
/// Firestore implementation of <see cref="IGrantRequestProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses flat collections with composite document IDs for efficient point reads:
/// {tenantId}_{userId}_{grantType}_{qualifier}
/// </para>
/// <para>
/// Uses SetAsync for upsert operations and UpdateAsync for soft deletes.
/// </para>
/// </remarks>
public sealed partial class FirestoreGrantService : IGrantRequestProvider, IGrantQueryProvider, IAsyncDisposable
{
	private readonly FirestoreAuthorizationOptions _options;
	private readonly ILogger<FirestoreGrantService> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreGrantService"/> class.
	/// </summary>
	/// <param name="options">The Firestore authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreGrantService(
		IOptions<FirestoreAuthorizationOptions> options,
		ILogger<FirestoreGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreGrantService"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreGrantService(
		FirestoreDb db,
		IOptions<FirestoreAuthorizationOptions> options,
		ILogger<FirestoreGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
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

		var docId = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var docRef = _collection.Document(docId);

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
			LogGrantRevoked(userId, tenantId ?? "null", grantType, qualifier);
		}
		else
		{
			// Hard delete
			_ = await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			LogGrantDeleted(userId, tenantId ?? "null", grantType, qualifier);
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

		var docId = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var docRef = _collection.Document(docId);

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

		var tenant = string.IsNullOrEmpty(tenantId) ? FirestoreGrantDocument.NullTenantSentinel : tenantId;

		var query = _collection
			.WhereEqualTo(FirestoreGrantDocument.TenantIdFieldName, tenant)
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

		var docId = FirestoreGrantDocument.CreateDocumentId(tenantId, userId, grantType, qualifier);
		var docRef = _collection.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		return FirestoreGrantDocument.FromSnapshot(snapshot);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query by user ID across all tenants
		var query = _collection
			.WhereEqualTo(FirestoreGrantDocument.UserIdFieldName, userId)
			.WhereEqualTo(FirestoreGrantDocument.IsRevokedFieldName, false);

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
	public async Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(grant);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = FirestoreGrantDocument.CreateDocumentId(grant.TenantId, grant.UserId, grant.GrantType, grant.Qualifier);
		var docRef = _collection.Document(docId);
		var data = FirestoreGrantDocument.ToDocumentData(grant);

		// SetAsync with merge behavior acts as upsert
		_ = await docRef.SetAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogGrantSaved(grant.UserId, grant.TenantId ?? "null", grant.GrantType, grant.Qualifier);
		return 1;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query by user ID across all tenants
		var query = _collection
			.WhereEqualTo(FirestoreGrantDocument.UserIdFieldName, userId)
			.WhereEqualTo(FirestoreGrantDocument.IsRevokedFieldName, false);

		var querySnapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var result = new Dictionary<string, object>();
		foreach (var doc in querySnapshot.Documents)
		{
			var grant = FirestoreGrantDocument.FromSnapshot(doc);
			if (grant is not null)
			{
				var key = $"{grant.GrantType}:{grant.Qualifier}";
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
		if (serviceType == typeof(IGrantQueryProvider))
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
		_initLock.Dispose();
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
