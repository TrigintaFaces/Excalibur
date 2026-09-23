// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.AuditLogging;

using Shouldly;

using Xunit;

namespace Excalibur.AuditLogging.Abstractions.Tests;

/// <summary>
/// Locks how often a chain verification resolves a signing key.
/// </summary>
/// <remarks>
/// <para>
/// This is a cost contract, not a correctness one, and it is worth a test because the cost is invisible in
/// every other test in this assembly. The in-box key provider is a dictionary read, so a per-record lookup
/// is free here and free in CI — while the provider this framework tells operators to register in
/// production is a KMS or secret-manager client. Under that configuration a per-record lookup is one
/// network round trip per audit record, so verifying a year of records is a year of round trips, and no
/// amount of lazy row reading or query tuning removes it.
/// </para>
/// <para>
/// The bound is the number of distinct key ids in the range — that is, the number of key rotations it
/// spans — not the number of records. These arms pin that, and they are written so that a regression to
/// per-record resolution fails them rather than merely slowing them down.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditChainKeyResolutionShould
{
	private const int ChainLength = 50;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	/// <summary>
	/// SAFETY: the resolution count must not grow with the range.
	/// </summary>
	/// <remarks>
	/// Paired with <see cref="StillVerifyTheChainItResolvedOneKeyFor" />, which is the arm that matters:
	/// a strategy that refused every link without resolving anything would satisfy this count trivially.
	/// </remarks>
	[Fact]
	public async Task ResolveOneKeyOnceForAWholeChain()
	{
		var provider = new CountingKeyProvider("k1", AuditIntegrityHarness.KeyA);
		var strategy = AuditIntegrityHarness.Strategy(provider);
		var links = await BuildChainAsync(strategy);

		provider.ResetCount();
		_ = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		provider.Calls.ShouldBe(
			1,
			$"{ChainLength} records signed by one key is one key resolution, not {ChainLength} — "
			+ "a KMS-backed provider pays a network round trip per call");
	}

	/// <summary>
	/// LIVENESS: the chain still verifies. Without this the count arm above is satisfied by a strategy
	/// that does nothing at all.
	/// </summary>
	[Fact]
	public async Task StillVerifyTheChainItResolvedOneKeyFor()
	{
		var provider = new CountingKeyProvider("k1", AuditIntegrityHarness.KeyA);
		var strategy = AuditIntegrityHarness.Strategy(provider);
		var links = await BuildChainAsync(strategy);

		var result = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeTrue("the chain is untampered and must verify");
	}

	/// <summary>
	/// Fail-closed on an unresolvable key, with the resolution count stated rather than implied.
	/// </summary>
	/// <remarks>
	/// HONEST LIMITATION, because this arm does NOT prove what it looks like it proves. It passes both with
	/// and without the key cache, so it is NOT a lock on negative caching. The reason is structural: the fold
	/// RETURNS on the first failed link, so an unresolvable key is looked up exactly once whether or not the
	/// absent result is cached. Verified by running this file against the pre-cache implementation - the two
	/// count arms above went red and this one stayed green.
	/// It is kept because fail-closed is worth pinning on its own. Caching the absent result remains correct
	/// and is retained for the single-record path and for any future fold that does not short-circuit, but
	/// nothing here binds it and this comment exists so the next reader does not believe otherwise.
	/// </remarks>
	[Fact]
	public async Task ResolveAnUnknownKeyOnlyOnceAndStillFailClosed()
	{
		var signing = new CountingKeyProvider("k1", AuditIntegrityHarness.KeyA);
		var strategy = AuditIntegrityHarness.Strategy(signing);
		var links = await BuildChainAsync(strategy);

		// Verify the same chain through a provider that resolves nothing.
		var blind = new CountingKeyProvider("other", AuditIntegrityHarness.KeyB);
		var blindStrategy = AuditIntegrityHarness.Strategy(blind);

		var result = await blindStrategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeFalse("an unresolvable key must fail closed");
		blind.Calls.ShouldBe(1, "the fold stops at the first failed link, so exactly one resolution is attempted");
	}

	/// <summary>
	/// The bound is distinct key ids, not one. A range spanning a rotation resolves both keys — proving the
	/// cache is keyed by key id rather than collapsing every id onto the first one it saw, which would
	/// verify the second half of a rotated chain against the wrong key.
	/// </summary>
	[Fact]
	public async Task ResolveEachDistinctKeyOnceWhenTheRangeSpansARotation()
	{
		var provider = new CountingKeyProvider("k1", AuditIntegrityHarness.KeyA);
		var beforeRotation = AuditIntegrityHarness.Strategy(provider);
		var firstHalf = await BuildChainAsync(beforeRotation, count: 5);

		// Rotate: a second strategy signs with k2, continuing the chain from the first half's last tag.
		var rotated = new CountingKeyProvider("k2", AuditIntegrityHarness.KeyB);
		rotated.AlsoResolve("k1", AuditIntegrityHarness.KeyA);
		var afterRotation = AuditIntegrityHarness.Strategy(rotated);

		var links = new List<AuditChainLink>(firstHalf);
		var priorTag = firstHalf[^1].Tag;
		for (var i = 0; i < 5; i++)
		{
			var content = AuditIntegrityHarness.Content($"post-rotation-{i}");
			var tag = await afterRotation.ComputeTagAsync(content, priorTag, Ct);
			links.Add(new AuditChainLink(content, tag, priorTag));
			priorTag = tag;
		}

		rotated.ResetCount();
		var result = await afterRotation.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeTrue("a chain spanning a rotation is still a valid chain");
		rotated.Calls.ShouldBe(2, "two distinct key ids is two resolutions — one per id, regardless of record count");
	}

	#region Helpers

	private static async Task<List<AuditChainLink>> BuildChainAsync(
		IAuditIntegrityStrategy strategy,
		int count = ChainLength)
	{
		var links = new List<AuditChainLink>(count);
		string? priorTag = null;

		for (var i = 0; i < count; i++)
		{
			var content = AuditIntegrityHarness.Content($"record-{i}");
			var tag = await strategy.ComputeTagAsync(content, priorTag, Ct);
			links.Add(new AuditChainLink(content, tag, priorTag));
			priorTag = tag;
		}

		return links;
	}

	private static async IAsyncEnumerable<AuditChainLink> AsChain(IEnumerable<AuditChainLink> links)
	{
		foreach (var link in links)
		{
			yield return link;
			await Task.Yield();
		}
	}

	/// <summary>
	/// Counts <see cref="IAuditSigningKeyProvider.GetSigningKeyAsync" /> calls. Verification-only: the
	/// current-key path is excluded from the count so that building a chain does not inflate it.
	/// </summary>
	private sealed class CountingKeyProvider(string currentKeyId, byte[] currentKey) : IAuditSigningKeyProvider
	{
		private readonly Dictionary<string, byte[]> _resolvable =
			new(StringComparer.Ordinal) { [currentKeyId] = currentKey };

		private int _calls;

		public int Calls => Volatile.Read(ref _calls);

		public void AlsoResolve(string keyId, byte[] key) => _resolvable[keyId] = key;

		public void ResetCount() => Volatile.Write(ref _calls, 0);

		public ValueTask<(string KeyId, byte[] Key)> GetCurrentSigningKeyAsync(CancellationToken cancellationToken)
			=> ValueTask.FromResult((currentKeyId, currentKey));

		public ValueTask<byte[]?> GetSigningKeyAsync(string keyId, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _calls);
			return ValueTask.FromResult(_resolvable.TryGetValue(keyId, out var key) ? key : null);
		}
	}

	#endregion
}
