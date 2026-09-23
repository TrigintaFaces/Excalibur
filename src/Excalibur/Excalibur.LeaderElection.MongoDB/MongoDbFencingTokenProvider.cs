// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.Options;

using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Excalibur.LeaderElection.MongoDB;

/// <summary>
/// MongoDB-backed <see cref="IFencingTokenProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Uses a per-resource counter document (keyed by the resource id) and MongoDB's atomic <c>$inc</c> via
/// <c>FindOneAndUpdateAsync</c> (upsert, return-after) as the monotonic
/// mint: the first leader receives <c>1</c> and every subsequent acquisition is strictly greater, without a
/// read-modify-write race that could mint two equal tokens. Mirrors the Postgres/Redis/SqlServer providers.
/// </para>
/// <para>
/// Validation is fail-closed against the current high-water mark (Kleppmann's fencing-token pattern): a token
/// is accepted only when it is at or above the stored value, so a stale leader whose lease was taken over by a
/// new leader (which advanced the counter) is rejected.
/// </para>
/// <para>
/// <b>Exhaustion:</b> the counter is a BSON 64-bit integer. MongoDB does NOT silently wrap a <c>$inc</c>
/// past <see cref="long.MaxValue"/> -- the server rejects the operation with a native
/// <c>MongoCommandException</c> ("Failed to apply $inc operations to current value..."), measured against a
/// real server. That is translated to <see cref="FencingTokenExhaustedException"/>, with the
/// native exception preserved as <see cref="Exception.InnerException"/>, mirroring the Redis and Postgres
/// providers' translation of their own native overflow errors. The <c>token &lt;= 0</c> check below remains
/// as a defensive backstop for a hypothetical driver/server version that returns rather than throws.
/// </para>
/// </remarks>
internal sealed class MongoDbFencingTokenProvider : IFencingTokenProvider
{
	private readonly IMongoCollection<FencingCounterDocument> _counters;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbFencingTokenProvider"/> class.
	/// </summary>
	/// <param name="client">The MongoDB client (same cluster as the leader election).</param>
	/// <param name="options">The MongoDB leader election options (database/collection naming).</param>
	public MongoDbFencingTokenProvider(IMongoClient client, IOptions<MongoDbLeaderElectionOptions> options)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);

		var value = options.Value;
		var database = client.GetDatabase(value.DatabaseName);
		_counters = database.GetCollection<FencingCounterDocument>(value.CollectionName + "_fencing");
	}

	/// <inheritdoc />
	public async ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		var filter = Builders<FencingCounterDocument>.Filter.Eq(d => d.ResourceId, resourceId);
		var update = Builders<FencingCounterDocument>.Update.Inc(d => d.Seq, 1L);
		var options = new FindOneAndUpdateOptions<FencingCounterDocument>
		{
			IsUpsert = true,
			ReturnDocument = ReturnDocument.After,
		};

		FencingCounterDocument document;
		try
		{
			document = await _counters.FindOneAndUpdateAsync(filter, update, options, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (MongoCommandException ex) when (ex.Message.Contains("$inc", StringComparison.Ordinal))
		{
			// The real server rejects an $inc that would overflow int64 rather than wrapping it (measured
			// against a real MongoDB). Translate so a consumer's fail-closed
			// catch(FencingTokenExhaustedException) relinquish path is honored rather than seeing a raw
			// MongoCommandException (a wrapped/reused fencing token would be a split-brain catastrophe).
			throw new FencingTokenExhaustedException(
				string.Format(
					CultureInfo.InvariantCulture,
					"MongoDB fencing token domain is exhausted for resource '{0}'.",
					resourceId),
				ex)
			{
				ResourceId = resourceId,
			};
		}

		var token = document.Seq;
		if (token <= 0)
		{
			// Defensive backstop: a non-positive result without a thrown exception would still be
			// non-monotonic. Fail closed rather than mint an unsafe token.
			throw new FencingTokenExhaustedException(
				string.Format(
					CultureInfo.InvariantCulture,
					"MongoDB fencing token domain is exhausted for resource '{0}'.",
					resourceId))
			{
				ResourceId = resourceId,
			};
		}

		return token;
	}

	/// <inheritdoc />
	public async ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		var filter = Builders<FencingCounterDocument>.Filter.Eq(d => d.ResourceId, resourceId);
		var document = await _counters.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

		// No counter document -> no token ever issued -> no active leader (idiomatic "no value" signal).
		return document is null ? null : document.Seq;
	}

	/// <inheritdoc />
	public async ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		// Fail-closed high-water-mark check: with no issued token nothing is valid; otherwise accept only
		// tokens at or above the current counter value, rejecting a stale leader's lower token.
		var current = await GetTokenAsync(resourceId, cancellationToken).ConfigureAwait(false);
		return current.HasValue && token >= current.Value;
	}

	/// <summary>
	/// Per-resource monotonic fencing counter document. The resource id is the document <c>_id</c>.
	/// </summary>
	internal sealed class FencingCounterDocument
	{
		/// <summary>Gets or sets the protected resource identifier (document id).</summary>
		[BsonId]
		public string ResourceId { get; set; } = string.Empty;

		/// <summary>Gets or sets the monotonic fencing counter value.</summary>
		public long Seq { get; set; }
	}
}
