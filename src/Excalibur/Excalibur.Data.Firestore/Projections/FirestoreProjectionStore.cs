// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.EventSourcing.Abstractions;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Projections;

/// <summary>
/// Google Cloud Firestore implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores projections as Firestore documents within a collection.
/// Each projection type uses a subcollection keyed by projection type name.
/// Supports dictionary-based filter queries translated to Firestore queries.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed class FirestoreProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly FirestoreDb _db;
	private readonly FirestoreProjectionStoreOptions _options;
	private readonly ILogger<FirestoreProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="db">The Firestore database instance.</param>
	/// <param name="options">The projection store options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreProjectionStore(
		FirestoreDb db,
		IOptions<FirestoreProjectionStoreOptions> options,
		ILogger<FirestoreProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc/>
	public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		var docRef = GetCollection().Document(id);
		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return null;
		}

		return DeserializeDocument(snapshot);
	}

	/// <inheritdoc/>
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);

		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var data = new Dictionary<string, object>
		{
			["data"] = json,
			["projectionType"] = _projectionType,
			["updatedAt"] = Timestamp.GetCurrentTimestamp(),
		};

		var docRef = GetCollection().Document(id);
		await docRef.SetAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		var docRef = GetCollection().Document(id);
		await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		Query query = GetCollection();

		if (options?.Take > 0)
		{
			query = query.Limit(options.Take.Value);
		}

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
		var results = new List<TProjection>();

		foreach (var doc in snapshot.Documents)
		{
			var projection = DeserializeDocument(doc);
			if (projection != null)
			{
				results.Add(projection);
			}
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		var snapshot = await GetCollection()
			.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
		return snapshot.Count;
	}

	private CollectionReference GetCollection()
	{
		return _db.Collection(_options.CollectionName).Document(_projectionType).Collection("items");
	}

	private TProjection? DeserializeDocument(DocumentSnapshot snapshot)
	{
		if (!snapshot.TryGetValue<string>("data", out var json) || json is null)
		{
			return null;
		}

		return JsonSerializer.Deserialize<TProjection>(json, _jsonOptions);
	}
}
