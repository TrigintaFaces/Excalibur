// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// <b>Exhaustion:</b> the counter is a BSON 64-bit integer. A <c>$inc</c> past <see cref="long.MaxValue"/>
/// wraps to a non-positive value; since tokens start at <c>1</c> and are strictly positive, a non-positive
/// result means the domain is exhausted and the provider fails closed with
/// <see cref="FencingTokenExhaustedException"/> rather than issuing a wrapped (non-monotonic) token.
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

		var document = await _counters.FindOneAndUpdateAsync(filter, update, options, cancellationToken)
			.ConfigureAwait(false);

		var token = document.Seq;
		if (token <= 0)
		{
			// int64 $inc wrapped past long.MaxValue -> exhausted. Fail closed rather than mint a
			// non-monotonic token that would let a stale leader validate as current (split-brain).
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
