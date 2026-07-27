// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

using Consul;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Consul;

/// <summary>
/// Consul-backed <see cref="IFencingTokenProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Uses a per-resource counter key in Consul's KV store and an atomic Check-And-Set (<c>CAS</c>) on the
/// key's <c>ModifyIndex</c> as the monotonic mint: read the current value + <c>ModifyIndex</c>, write
/// <c>value + 1</c> guarded by that <c>ModifyIndex</c>, and retry on a lost CAS race. The first leader
/// receives <c>1</c> and every subsequent acquisition is strictly greater — a concurrent minter loses the
/// CAS and retries against the advanced value, so two equal tokens can never be issued.
/// </para>
/// <para>
/// Validation is fail-closed against the current high-water mark (Kleppmann's fencing-token pattern): a
/// token is accepted only when it is at or above the stored value, rejecting a stale leader's lower token.
/// </para>
/// <para>
/// <b>Exhaustion:</b> the counter is an <see cref="long"/>. When the stored value has reached
/// <see cref="long.MaxValue"/> the next mint would overflow, so the provider fails closed with
/// <see cref="FencingTokenExhaustedException"/> rather than wrapping (a wrapped, non-monotonic token would
/// let a stale leader validate as current — split-brain).
/// </para>
/// </remarks>
internal sealed class ConsulFencingTokenProvider : IFencingTokenProvider
{
	// Bounded retry budget for the CAS mint under concurrent contention (lost-race, NOT exhaustion).
	private const int MaxCasAttempts = 8;

	private readonly IConsulClient _consulClient;
	private readonly string _keyPrefix;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsulFencingTokenProvider"/> class.
	/// </summary>
	/// <param name="consulClient">The Consul client (same cluster as the leader election).</param>
	/// <param name="options">The Consul leader election options (KV key prefix).</param>
	public ConsulFencingTokenProvider(IConsulClient consulClient, IOptions<ConsulLeaderElectionOptions> options)
	{
		ArgumentNullException.ThrowIfNull(consulClient);
		ArgumentNullException.ThrowIfNull(options);

		_consulClient = consulClient;
		_keyPrefix = options.Value.KeyPrefix;
	}

	/// <inheritdoc />
	public async ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		var key = CounterKey(resourceId);

		for (var attempt = 1; attempt <= MaxCasAttempts; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var existing = await _consulClient.KV.Get(key, cancellationToken).ConfigureAwait(false);

			var current = 0L;
			var modifyIndex = 0UL;
			if (existing.Response is { } pair)
			{
				current = ParseValue(pair.Value);
				modifyIndex = pair.ModifyIndex;
			}

			if (current >= long.MaxValue)
			{
				// Next mint would overflow the int64 domain -> fail closed (never wrap a fencing token).
				throw new FencingTokenExhaustedException(
					string.Format(
						CultureInfo.InvariantCulture,
						"Consul fencing token domain is exhausted for resource '{0}'.",
						resourceId))
				{
					ResourceId = resourceId,
				};
			}

			var next = current + 1;

			// CAS: ModifyIndex==0 creates the key only if absent (first mint); otherwise the write succeeds
			// only if the key is unchanged since the read, so a concurrent minter loses and retries.
			var kvPair = new KVPair(key)
			{
				Value = Encoding.UTF8.GetBytes(next.ToString(CultureInfo.InvariantCulture)),
				ModifyIndex = modifyIndex,
			};

			var cas = await _consulClient.KV.CAS(kvPair, cancellationToken).ConfigureAwait(false);
			if (cas.Response)
			{
				return next;
			}
		}

		// Bounded CAS retries exhausted under contention — transient, distinct from domain exhaustion.
		throw new InvalidOperationException(
			string.Format(
				CultureInfo.InvariantCulture,
				"Failed to mint a Consul fencing token for resource '{0}' after {1} CAS attempts due to contention.",
				resourceId,
				MaxCasAttempts));
	}

	/// <inheritdoc />
	public async ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		var existing = await _consulClient.KV.Get(CounterKey(resourceId), cancellationToken).ConfigureAwait(false);

		// No counter key -> no token ever issued -> no active leader (idiomatic "no value" signal).
		return existing.Response is { } pair ? ParseValue(pair.Value) : null;
	}

	/// <inheritdoc />
	public async ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		// Fail-closed high-water-mark check: with no issued token nothing is valid; otherwise accept only
		// tokens at or above the current value, rejecting a stale leader's lower token.
		var current = await GetTokenAsync(resourceId, cancellationToken).ConfigureAwait(false);
		return current.HasValue && token >= current.Value;
	}

	private string CounterKey(string resourceId) => $"{_keyPrefix}/fencing/{resourceId}";

	private static long ParseValue(byte[]? value)
	{
		if (value is null || value.Length == 0)
		{
			return 0L;
		}

		return long.TryParse(Encoding.UTF8.GetString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: 0L;
	}
}
