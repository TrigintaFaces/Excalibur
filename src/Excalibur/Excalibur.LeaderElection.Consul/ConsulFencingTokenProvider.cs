// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// <b>Unreadable mark:</b> a counter key that exists but holds no value, or a value that is not a base-10
/// integer, is a <i>failure</i> and never a number — both the mint and the read fail with
/// <see cref="InvalidDataException"/>. Substituting a value would fabricate a high-water mark, and the
/// resulting write would move the fence backward through a CAS that legitimately succeeds. Reporting it as
/// "no token issued" is the same defect: that state licenses the first mint to start at <c>1</c>. Only an
/// <i>absent</i> key means no token was ever issued, so only an absent key yields <see langword="null"/>
/// from <see cref="GetTokenAsync"/>; the caller must relinquish rather than lead on a fence it could not
/// establish.
/// </para>
/// <para>
/// <b>Exhaustion:</b> the counter is an <see cref="long"/>. When the stored value has reached
/// <see cref="long.MaxValue"/> the next mint would overflow, so the provider fails closed with
/// <see cref="FencingTokenExhaustedException"/> rather than wrapping (a wrapped, non-monotonic token would
/// let a stale leader validate as current — split-brain).
/// </para>
/// </remarks>
internal sealed class ConsulFencingTokenProvider : IFencingTokenIssuer
{
	// Bounded retry budget for the CAS mint under concurrent contention (lost-race, NOT exhaustion).
	private const int MaxCasAttempts = 8;

	private readonly IConsulClient _consulClient;
	private readonly string _keyPrefix;
	private readonly string? _datacenter;
	private readonly string? _token;

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
		var value = options.Value;
		_keyPrefix = value.KeyPrefix;
		_datacenter = value.Datacenter;
		_token = value.Token;
	}

	/// <inheritdoc />
	public async ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		var key = CounterKey(resourceId);

		for (var attempt = 1; attempt <= MaxCasAttempts; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var existing = await _consulClient.KV.Get(key, ConsulElectionConfiguration.LeaderQueryOptions(_datacenter, _token), cancellationToken).ConfigureAwait(false);

			var current = 0L;
			var modifyIndex = 0UL;
			if (existing.Response is { } pair)
			{
				// Propagates when the mark is unreadable: minting onto an unreadable predecessor IS the reset,
				// because the CAS below carries a correct ModifyIndex and therefore succeeds.
				current = ParseValue(pair.Value, key);
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

			var cas = await _consulClient.KV.CAS(kvPair, ConsulElectionConfiguration.WriteOptions(_datacenter, _token), cancellationToken).ConfigureAwait(false);
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

		var key = CounterKey(resourceId);
		var existing = await _consulClient.KV.Get(key, ConsulElectionConfiguration.LeaderQueryOptions(_datacenter, _token), cancellationToken).ConfigureAwait(false);

		// No counter key -> no token ever issued -> no active leader (idiomatic "no value" signal). A key
		// that exists but cannot be READ still throws -- ParseValue draws that line and keeps it.
		// A stored ZERO is not a mark. The contract requires null -- never zero -- when no token has been
		// issued, INCLUDING when the backing store's counter exists but was never advanced, and the
		// reason is stated there: validation is a high-water comparison, so a mark reported as zero has
		// a value, passes the has-a-mark term, and then admits EVERY non-negative token presented
		// against it, a fabricated zero included. The state that must refuse everything is exactly the
		// state that would otherwise admit everything. This provider never writes zero, so the state
		// arrives from outside -- a restore, a migration, an external writer to the consumer's own
		// store, or a counter written by an older build -- and the comparison below is what makes it
		// inexpressible as a mark rather than merely unlikely.
		//
		// Note the three outcomes stay distinct here: absent -> null, unreadable -> throw, readable zero
		// -> null. ParseValue already refused an empty payload; it did not refuse a readable zero.
		return existing.Response is { } pair && ParseValue(pair.Value, key) is var mark && mark > 0L
			? mark
			: null;
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

	// An unreadable mark is a FAILURE, never a number. Three outcomes must stay distinguishable -- a value,
	// UNSET, and "I could not tell" -- and long cannot express the third, so returning any number here
	// fabricates a high-water mark. Returning 0 was the worst available fabrication: the mint then CASes
	// from it at the ModifyIndex it just read, so the write SUCCEEDS, the mark moves backward to 1, and every
	// token any prior leader holds validates from then on -- each admission logged as a success. There is no
	// good number, so this refuses instead.
	//
	// Treating it as UNSET would be the same defect by another route: UNSET means no token was ever issued,
	// which licenses the first mint to start at 1. Absent and unreadable differ in whether a predecessor may
	// exist, and that is the whole question -- so only an ABSENT key yields null, never this.
	//
	// InvalidDataException names the cause, and costs no retry spin: this provider's election mints ONCE and
	// catches only the exhaustion signal, so an unreadable mark propagates out of the acquisition attempt and
	// leadership is never announced. Reporting an exhausted token domain would take the relinquish branch
	// instead, but it would log and surface "token domain exhausted" for a counter that is fine, sending an
	// operator to reset a counter when the remedy is to repair the payload. Both refuse to lead; only one
	// names the cause. Known gap on the honest path: the acquisition's own error handler does not release the
	// KV lock it had already taken, so failover waits out the session TTL -- safe, but slower than the
	// exhaustion branch, and it is the generic mint-failure cleanup that needs widening, not this signal.
	private static long ParseValue(byte[]? value, string key)
	{
		// Reached only for a key that EXISTS, so a null or zero-length payload is a present mark we cannot
		// read -- Consul renders a valueless key this way, and this provider only ever writes a decimal.
		if (value is null || value.Length == 0)
		{
			throw new InvalidDataException(
				string.Format(
					CultureInfo.InvariantCulture,
					"The Consul fencing counter at '{0}' exists but holds no value, so the current high-water mark cannot be established.",
					key));
		}

		var text = Encoding.UTF8.GetString(value);
		return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: throw new InvalidDataException(
				string.Format(
					CultureInfo.InvariantCulture,
					"The Consul fencing counter at '{0}' is not a base-10 integer, so the current high-water mark cannot be established.",
					key));
	}
}
