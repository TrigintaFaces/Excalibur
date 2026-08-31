// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;

using Shouldly;

using Xunit;

namespace Excalibur.AuditLogging.Abstractions.Tests;

/// <summary>
/// Locks for the single-record half of the keyed-MAC integrity strategy: what a tag covers, and every way a
/// tag must be refused.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditIntegrityStrategyShould
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	/// <summary>
	/// The liveness arm. Every rejection arm below is satisfied by a strategy that refuses everything —
	/// which would also refuse an untampered audit trail and report a clean system as compromised.
	/// </summary>
	[Fact]
	public async Task VerifyATagItJustProduced()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var content = AuditIntegrityHarness.Content("record-1");

		var tag = await strategy.ComputeTagAsync(content, priorTag: null, Ct);

		(await strategy.VerifyAsync(content, priorTag: null, tag, Ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task EmitAVersionedKeyIdentifiedTag()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);

		var tag = await strategy.ComputeTagAsync(AuditIntegrityHarness.Content("r"), priorTag: null, Ct);

		var parts = tag.Split(':');
		parts.Length.ShouldBe(3);
		parts[0].ShouldBe("v1");
		parts[1].ShouldBe("k1");
		Convert.FromBase64String(parts[2]).Length.ShouldBe(32);
	}

	/// <summary>
	/// The core tamper-evidence property: rewriting the record's contents invalidates its tag.
	/// </summary>
	[Fact]
	public async Task RefuseATagWhoseContentWasAltered()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var original = AuditIntegrityHarness.Content("transfer 100");

		var tag = await strategy.ComputeTagAsync(original, priorTag: null, Ct);

		var altered = AuditIntegrityHarness.Content("transfer 900");
		(await strategy.VerifyAsync(altered, priorTag: null, tag, Ct)).ShouldBeFalse();
	}

	/// <summary>
	/// The tag covers the predecessor as well as the contents. Without this, records verify individually
	/// while being freely reordered — the chain would be decoration.
	/// </summary>
	[Fact]
	public async Task RefuseATagWhosePriorTagWasChanged()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var content = AuditIntegrityHarness.Content("record-2");

		var tag = await strategy.ComputeTagAsync(content, priorTag: "v1:k1:AAAA", Ct);

		(await strategy.VerifyAsync(content, priorTag: "v1:k1:BBBB", tag, Ct)).ShouldBeFalse();
	}

	/// <summary>
	/// A genesis record and a chained record are distinct positions in the trail, so a genesis tag must not
	/// verify once a predecessor is claimed — otherwise records could be spliced in front of the trail.
	/// </summary>
	[Fact]
	public async Task RefuseAGenesisTagOnceAPredecessorIsClaimed()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var content = AuditIntegrityHarness.Content("record-1");

		var genesisTag = await strategy.ComputeTagAsync(content, priorTag: null, Ct);

		(await strategy.VerifyAsync(content, priorTag: "v1:k1:AAAA", genesisTag, Ct)).ShouldBeFalse();
	}

	/// <summary>
	/// The key is what makes the tag unforgeable. A tag that verified under any key would let anyone who
	/// can write to the audit store re-tag a record they had just rewritten.
	/// </summary>
	[Fact]
	public async Task RefuseATagProducedUnderADifferentKey()
	{
		var content = AuditIntegrityHarness.Content("record-1");
		var signedWithA = await AuditIntegrityHarness.StrategyWith("shared-id", AuditIntegrityHarness.KeyA)
			.ComputeTagAsync(content, priorTag: null, Ct);

		// Same key id, different key material — so the strategy resolves a key and still must refuse.
		var strategyWithB = AuditIntegrityHarness.StrategyWith("shared-id", AuditIntegrityHarness.KeyB);

		(await strategyWithB.VerifyAsync(content, priorTag: null, signedWithA, Ct)).ShouldBeFalse();
	}

	/// <summary>
	/// Fail closed on an unresolvable key: a rotated-out or unavailable key makes a record
	/// <em>unverifiable</em>, and unverifiable must never be reported as verified.
	/// </summary>
	[Fact]
	public async Task RefuseATagWhoseKeyCannotBeResolved()
	{
		var keyProvider = new AuditIntegrityHarness.StubKeyProvider("k1", AuditIntegrityHarness.KeyA);
		var strategy = AuditIntegrityHarness.Strategy(keyProvider);
		var content = AuditIntegrityHarness.Content("record-1");

		var tag = await strategy.ComputeTagAsync(content, priorTag: null, Ct);
		keyProvider.StopResolving("k1");

		(await strategy.VerifyAsync(content, priorTag: null, tag, Ct)).ShouldBeFalse();
	}

	/// <summary>
	/// Key rotation is the reason the key id is embedded in the tag: records written under a retired key
	/// must keep verifying once a new key becomes current.
	/// </summary>
	[Fact]
	public async Task VerifyARecordWrittenUnderARetiredKey()
	{
		var oldKeyProvider = new AuditIntegrityHarness.StubKeyProvider("k-old", AuditIntegrityHarness.KeyA);
		var tag = await AuditIntegrityHarness.Strategy(oldKeyProvider)
			.ComputeTagAsync(AuditIntegrityHarness.Content("historic"), priorTag: null, Ct);

		var rotated = new AuditIntegrityHarness.StubKeyProvider("k-new", AuditIntegrityHarness.KeyB);
		rotated.AlsoResolve("k-old", AuditIntegrityHarness.KeyA);

		var verified = await AuditIntegrityHarness.Strategy(rotated)
			.VerifyAsync(AuditIntegrityHarness.Content("historic"), priorTag: null, tag, Ct);

		verified.ShouldBeTrue();
	}

	/// <summary>
	/// A tag that is not a tag is unverifiable, and unverifiable is refused. Each case is a distinct way the
	/// parser can be handed something that is not a <c>v1:{keyId}:{mac}</c> token.
	/// </summary>
	[Theory]
	[InlineData("")]
	[InlineData("not-a-tag")]
	[InlineData("v1:k1")]                       // missing the mac segment
	[InlineData("v1:k1:AAAA:extra")]            // one segment too many
	[InlineData("v2:k1:AAAA")]                  // unknown tag version
	[InlineData("v1::AAAA")]                    // empty key id
	[InlineData("v1:k1:!!!not-base64!!!")]      // mac is not base64
	[InlineData("v1:k1:AAAAAAAA")]              // well-formed base64, wrong mac length
	public async Task RefuseAMalformedTag(string tag)
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);

		(await strategy.VerifyAsync(AuditIntegrityHarness.Content("r"), priorTag: null, tag, Ct)).ShouldBeFalse();
	}

	/// <summary>
	/// There is no unkeyed path. When no key is available the compute side throws rather than emitting an
	/// unprotected tag, because a record that looks tagged and is not is worse than one that is plainly not.
	/// </summary>
	[Fact]
	public async Task ThrowRatherThanEmitAnUnkeyedTag()
	{
		var strategy = AuditIntegrityHarness.Strategy(
			new AuditIntegrityHarness.StubKeyProvider("k1", AuditIntegrityHarness.KeyA) { ThrowOnCurrent = true });

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await strategy.ComputeTagAsync(AuditIntegrityHarness.Content("r"), priorTag: null, Ct));
	}

	/// <summary>
	/// The tag is colon-delimited, so a colon-bearing key id would produce a token that parses into the
	/// wrong fields. It is refused at write time, where the operator can still act on it.
	/// </summary>
	[Fact]
	public async Task RefuseAKeyIdentifierThatWouldCorruptTheTagFormat()
	{
		var strategy = AuditIntegrityHarness.Strategy(
			new AuditIntegrityHarness.MalformedKeyIdProvider("tenant:1"));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await strategy.ComputeTagAsync(AuditIntegrityHarness.Content("r"), priorTag: null, Ct));
	}
}
