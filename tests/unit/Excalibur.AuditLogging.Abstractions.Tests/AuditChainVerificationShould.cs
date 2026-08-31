// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;

using Shouldly;

using Xunit;

namespace Excalibur.AuditLogging.Abstractions.Tests;

/// <summary>
/// Locks for chain verification — the part of the design that makes <em>deletion</em> detectable, not just
/// forgery. Each arm removes or alters one thing and states which break the reader is owed, because "the
/// chain failed" and "the third record was rewritten while the rest are intact" call for different work.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditChainVerificationShould
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static async Task<List<AuditChainLink>> BuildChainAsync(
		IAuditIntegrityStrategy strategy,
		params string[] records)
	{
		var links = new List<AuditChainLink>(records.Length);
		string? priorTag = null;

		foreach (var record in records)
		{
			var content = AuditIntegrityHarness.Content(record);
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
	/// The liveness arm, and it carries the whole class: every break arm below is satisfied by a verifier
	/// that returns "broken" unconditionally, which in production means a clean audit trail reported as
	/// tampered — a false alarm that costs an investigation each time it fires.
	/// </summary>
	[Fact]
	public async Task ReportAnIntactChainAsValid()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3");

		var result = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeTrue();
		result.Break.ShouldBe(AuditChainBreak.None);
		result.FirstBrokenIndex.ShouldBe(-1);
	}

	[Fact]
	public async Task ReportAnEmptyRangeAsValid()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);

		var result = await strategy.VerifyChainAsync(
			AsChain([]), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeTrue();
	}

	/// <summary>
	/// A record rewritten in place, with its linkage left alone. The verifier must say so specifically: the
	/// records around it are intact, and the reader needs to know it is one record's contents that moved.
	/// </summary>
	[Fact]
	public async Task ReportContentAltered_WhenARecordIsRewrittenInPlace()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3");

		links[1] = links[1] with { CanonicalContent = AuditIntegrityHarness.Content("rewritten") };

		var result = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeFalse();
		result.FirstBrokenIndex.ShouldBe(1);
		result.Break.ShouldBe(AuditChainBreak.ContentAltered);
	}

	/// <summary>
	/// A record removed from the middle. The survivor that followed it still names the record that is now
	/// gone, and that surviving claim is what makes the deletion visible.
	/// </summary>
	[Fact]
	public async Task ReportPredecessorMismatch_WhenARecordIsRemovedFromTheMiddle()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3");

		links.RemoveAt(1);

		var result = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeFalse();
		result.FirstBrokenIndex.ShouldBe(1);
		result.Break.ShouldBe(AuditChainBreak.PredecessorMismatch);
	}

	/// <summary>
	/// The stored linkage value moved on its own. The MAC does not cover the stored copy — it covers the
	/// prior tag supplied at write time — so this is the one field an attacker can edit without invalidating
	/// a MAC, and comparing it is what turns it from an unread column into a checked one.
	/// </summary>
	[Fact]
	public async Task ReportStoredLinkageAltered_WhenOnlyTheStoredClaimIsEdited()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3");

		links[2] = links[2] with { StoredPriorTag = "v1:k1:" + Convert.ToBase64String(new byte[32]) };

		var result = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeFalse();
		result.FirstBrokenIndex.ShouldBe(2);
		result.Break.ShouldBe(AuditChainBreak.StoredLinkageAltered);
	}

	/// <summary>
	/// Clearing a record's tag must not be a way to opt out of the chain. Reported, never skipped —
	/// otherwise blanking one column achieves what deleting the row could not.
	/// </summary>
	[Fact]
	public async Task ReportUntaggedRecord_RatherThanSkippingIt()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3");

		links[1] = links[1] with { Tag = string.Empty };

		var result = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeFalse();
		result.FirstBrokenIndex.ShouldBe(1);
		result.Break.ShouldBe(AuditChainBreak.UntaggedRecord);
	}

	/// <summary>
	/// The left edge. Records removed from the <em>front</em> of a range leave the survivors chaining
	/// perfectly to one another, so nothing inside the range shows the loss — only the anchor does.
	/// </summary>
	[Fact]
	public async Task DetectATruncatedFront_ByBindingTheFirstLinkToTheAnchor()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3");

		// The forger presents r2..r3 as if the range began there. Internally consistent, and wrong.
		var truncated = links.Skip(1).ToList();
		var genuineAnchor = links[0].Tag;

		var withoutAnchor = await strategy.VerifyChainAsync(
			AsChain(truncated), anchorPriorTag: null, successor: null, Ct);
		var withAnchor = await strategy.VerifyChainAsync(
			AsChain(truncated), anchorPriorTag: genuineAnchor, successor: null, Ct);

		// Anchored at the record that really precedes the range, the truncation is visible.
		withAnchor.IsValid.ShouldBeTrue();
		withoutAnchor.IsValid.ShouldBeFalse();
		withoutAnchor.FirstBrokenIndex.ShouldBe(0);
	}

	/// <summary>
	/// The right edge. Records removed from the <em>end</em> leave nothing inside the range that mentions
	/// them; the record that followed is the only one still carrying the tag that named what was there.
	/// </summary>
	[Fact]
	public async Task DetectATruncatedTail_ByPinningTheSuccessor()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3", "r4");

		var successor = links[3];
		var truncated = links.Take(2).ToList(); // r3 removed from the end of the range

		var withoutSuccessor = await strategy.VerifyChainAsync(
			AsChain(truncated), anchorPriorTag: null, successor: null, Ct);
		var withSuccessor = await strategy.VerifyChainAsync(
			AsChain(truncated), anchorPriorTag: null, successor, Ct);

		// Unpinned, the surviving prefix is internally perfect — which is exactly the danger.
		withoutSuccessor.IsValid.ShouldBeTrue();

		withSuccessor.IsValid.ShouldBeFalse();
		withSuccessor.Break.ShouldBe(AuditChainBreak.SuccessorLinkBroken);
		withSuccessor.FirstBrokenIndex.ShouldBe(2);
	}

	/// <summary>
	/// The successor arm's own liveness half: an untruncated range with its real successor must verify, or
	/// the pin would report every complete range as broken.
	/// </summary>
	[Fact]
	public async Task AcceptACompleteRange_WhenItsSuccessorChainsToIt()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3", "r4");

		var result = await strategy.VerifyChainAsync(
			AsChain(links.Take(3)), anchorPriorTag: null, successor: links[3], Ct);

		result.IsValid.ShouldBeTrue();
		result.Break.ShouldBe(AuditChainBreak.None);
	}

	/// <summary>
	/// Two adjacent records swapped. Reordering is the attack the chain exists to catch, and it must not be
	/// mistaken for the records simply having different contents.
	/// </summary>
	[Fact]
	public async Task DetectReorderedRecords()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3");

		(links[1], links[2]) = (links[2], links[1]);

		var result = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeFalse();
		result.FirstBrokenIndex.ShouldBe(1);
	}

	/// <summary>
	/// A genesis record's absent predecessor means the same thing whether a backend writes <c>null</c> or an
	/// empty string. Treating the two spellings as different values would report an intact trail as broken
	/// on any store that spells absence the other way.
	/// </summary>
	[Fact]
	public async Task TreatANullAndAnEmptyStoredPriorTagAsTheSameClaim()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2");

		links[0] = links[0] with { StoredPriorTag = string.Empty };

		var result = await strategy.VerifyChainAsync(AsChain(links), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeTrue();
	}

	/// <summary>
	/// The chain is enumerated once and only as far as the first break, so a compliance pass over a year of
	/// records does not stream the remainder after it already has its answer.
	/// </summary>
	[Fact]
	public async Task StopEnumeratingAtTheFirstBreak()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);
		var links = await BuildChainAsync(strategy, "r1", "r2", "r3", "r4", "r5");
		links[1] = links[1] with { Tag = string.Empty };

		var yielded = 0;

		async IAsyncEnumerable<AuditChainLink> Counted()
		{
			foreach (var link in links)
			{
				yielded++;
				yield return link;
				await Task.Yield();
			}
		}

		var result = await strategy.VerifyChainAsync(Counted(), anchorPriorTag: null, successor: null, Ct);

		result.IsValid.ShouldBeFalse();
		yielded.ShouldBe(2);
	}

	[Fact]
	public async Task Reject_ANullChain()
	{
		var strategy = AuditIntegrityHarness.StrategyWith("k1", AuditIntegrityHarness.KeyA);

		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await strategy.VerifyChainAsync(null!, anchorPriorTag: null, successor: null, Ct));
	}
}
