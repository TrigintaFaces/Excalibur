// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection.Fencing;

using StackExchange.Redis;

namespace Excalibur.LeaderElection.Redis;

/// <summary>
/// Redis-backed <see cref="IFencingTokenProvider"/> reference implementation.
/// </summary>
/// <remarks>
/// <para>
/// Uses Redis <c>INCR</c> as the atomic monotonic mint: each issuance increments a
/// per-resource counter (<c>fencing:{resourceId}</c>). <c>INCR</c> on a missing key
/// initializes to <c>1</c>, so the first leader receives <c>1</c> and every subsequent
/// leadership acquisition is strictly greater — the monotonic invariant holds without a
/// separate initialization step, and without any read-modify-write race that could mint two
/// equal tokens.
/// </para>
/// <para>
/// Validation is fail-closed against the current high-water mark: a token is accepted only
/// when it is at or above the stored counter, so a stale leader whose lease has been taken
/// over by a new leader (which advanced the counter) is rejected. This is the distributed
/// systems fencing-token pattern described by Martin Kleppmann.
/// </para>
/// </remarks>
internal sealed class RedisFencingTokenProvider : IFencingTokenProvider
{
	/// <summary>The Redis key prefix under which per-resource fencing counters are stored.</summary>
	private const string KeyPrefix = "fencing:";

	private readonly IConnectionMultiplexer _redis;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisFencingTokenProvider"/> class.
	/// </summary>
	/// <param name="redis">The Redis connection multiplexer.</param>
	public RedisFencingTokenProvider(IConnectionMultiplexer redis)
	{
		_redis = redis ?? throw new ArgumentNullException(nameof(redis));
	}

	/// <inheritdoc />
	public async ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		cancellationToken.ThrowIfCancellationRequested();

		// INCR is atomic + monotonic; a missing key initializes to 1. The returned value is the
		// newly minted token — strictly greater than any previously issued token for this resource.
		var db = _redis.GetDatabase();
		return await db.StringIncrementAsync(KeyPrefix + resourceId).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		cancellationToken.ThrowIfCancellationRequested();

		// null = no token has ever been issued for this resource (no active leader). Never a
		// fabricated/sentinel value — the idiomatic "no value" signal (ADR-339 Decision 2).
		var db = _redis.GetDatabase();
		var value = await db.StringGetAsync(KeyPrefix + resourceId).ConfigureAwait(false);
		return value.HasValue ? (long)value : null;
	}

	/// <inheritdoc />
	public async ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		// Fail-closed high-water-mark check: with no issued token nothing is valid; otherwise accept
		// only tokens at or above the current counter, rejecting a stale leader's lower token.
		var current = await GetTokenAsync(resourceId, cancellationToken).ConfigureAwait(false);
		return current.HasValue && token >= current.Value;
	}
}
