// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Holds the mapping from a stored tenant term back to the identifier that was signed to a single
/// definition, and pins what that definition returns for every spelling of "no tenant".
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is integrity-relevant.</b> The audit integrity tag is computed over the record as supplied,
/// where an untenanted event carries a null tenant, while the column stores the reserved sentinel. Any
/// site that turns the stored term back into the signed one is therefore part of the integrity contract:
/// if two such sites disagree about which inputs mean "no tenant", a record can be canonicalised one way
/// on the way in and another on the way out, and verify as tampered while nothing touched it.
/// </para>
/// <para>
/// <b>What drifted.</b> The audit key compared against the sentinel alone. The dead-letter replay path, in
/// a different assembly, additionally treated an empty term as absent. They agreed on every input except
/// the empty and whitespace ones -- which is the shape of divergence that survives review, because the
/// disagreement is invisible until the one input that separates them shows up.
/// </para>
/// <para>
/// <b>Why these arms are not vacuous even though both sites now call one function.</b> They assert the
/// mapping's observable values, not that a particular call is made. Re-hand-rolling either site as a bare
/// <c>== sentinel</c> comparison -- the exact regression this collapses -- turns the empty and whitespace
/// arms RED, because that comparison returns those terms unchanged instead of null.
/// </para>
/// <para>
/// <b>Scope.</b> The replay path's behaviour through a real dead-letter queue is bound separately, against
/// a real server, by the tenant-provenance suite; these arms bind the canonicalisation itself.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SignedTenantCanonicalisationShould
{
	private const string Sentinel = "__untenanted__";

	/// <summary>
	/// SAFETY. Every spelling of an absent tenant canonicalises to null, so no two sites can disagree
	/// about which of them meant "no tenant".
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(Sentinel)]
	public void CollapseEverySpellingOfUntenantedToNull(string? stored)
	{
		KeyedTenantPartition.ToSignedTenantId(stored).ShouldBeNull(
			"the store's own contract treats null, empty, whitespace and the sentinel as the same "
			+ "untenanted row, so the value that was signed for all four is null. A site that folds only "
			+ "the sentinel disagrees with this one about the other three.");
	}

	/// <summary>
	/// LIVENESS. A real tenant survives the mapping unchanged.
	/// </summary>
	/// <remarks>
	/// Without this arm every assertion in the file is satisfied by a canonicalisation that returns null
	/// for everything -- which would verify cleanly and silently strip the tenant from every signed record
	/// and every dead-letter replay, sending tenanted work into no tenant's scope.
	/// </remarks>
	[Theory]
	[InlineData("t-a")]
	[InlineData("__default__")]
	[InlineData("  padded-but-real  ")]
	public void ReturnARealTenantUnchanged(string stored)
	{
		KeyedTenantPartition.ToSignedTenantId(stored).ShouldBe(
			stored,
			"a resolved tenant is the value that was signed and the scope a replay must re-enter. A "
			+ "mapping that returned null here would pass every safety arm above while making every "
			+ "tenanted record untenanted.");
	}

	/// <summary>
	/// SAFETY. The audit chain key reports the same mapping, across the whole input set, rather than
	/// carrying a second copy of the decision.
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(Sentinel)]
	[InlineData("t-a")]
	public void AgreeWithTheAuditChainKey(string? stored)
	{
		AuditChainKey.SignedTenantId(stored).ShouldBe(
			KeyedTenantPartition.ToSignedTenantId(stored),
			"one integrity-relevant canonicalisation, one definition. When the audit key answers this "
			+ "differently from the partition it is signing against, an untouched trail verifies as "
			+ "tampered.");
	}

	/// <summary>
	/// SAFETY. The mapping is the inverse of the one that wrote the term: a value folded to storage and
	/// back is either the original tenant or null, never a third thing.
	/// </summary>
	/// <remarks>
	/// This is the round trip the two sites exist to serve. It fails if either direction grows a spelling
	/// the other does not share.
	/// </remarks>
	[Theory]
	[InlineData(null, null)]
	[InlineData("", null)]
	[InlineData(Sentinel, null)]
	[InlineData("t-a", "t-a")]
	public void InvertTheStoredForm(string? supplied, string? expected)
	{
		var storedTerm = KeyedTenantPartition.FromStoredValue(supplied).TenantId;

		KeyedTenantPartition.ToSignedTenantId(storedTerm).ShouldBe(expected);
	}
}
