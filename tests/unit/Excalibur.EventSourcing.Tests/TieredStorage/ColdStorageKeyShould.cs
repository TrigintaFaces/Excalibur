// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Buffers.Text;
using System.Text;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// Author≠implementer lock on the cold-storage tenant key segment.
/// </summary>
/// <remarks>
/// <para>
/// The segment is the sole thing keeping one tenant's archived events out of another's cold-storage
/// namespace. The property it must hold is <b>injectivity</b>: distinct tenants MUST produce distinct
/// segments. If two tenants can be mapped to one segment, then one tenant's archive can satisfy the
/// other's watermark check, and the archive service will delete hot events that were never archived —
/// history loss reported as success.
/// </para>
/// <para>
/// Injectivity is asserted here as a <i>property</i>, over an adversarial corpus chosen so that any
/// character-substitution scheme collides on it, rather than by asserting a particular key format.
/// A test that pinned the format would pass for a scheme that is correctly formatted and still lossy.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ColdStorageKeyShould
{
	/// <summary>
	/// Tenants that differ only in characters a sanitizing implementation is tempted to rewrite.
	/// Any scheme that maps separators to a common replacement collapses these onto one segment.
	/// </summary>
	private static readonly (string First, string Second)[] AdversarialPairs =
	[
		// The measured collision: '/', '\' and '_' all rewritten to '_'.
		("a/b", "a_b"),
		(@"a\b", "a_b"),
		("a/b", @"a\b"),

		// Boundary ambiguity: the tenant may contain the delimiter itself.
		("1!x", "1!y"),
		("3!ab", "ab"),

		// Length-prefix aliasing: a tenant whose text begins with a count.
		("2!ab", "22!b"),

		// Ordinary distinctness, including case, which is not a collision but must not be folded.
		("tenant-a", "tenant-b"),
		("Tenant", "tenant"),
	];

	public static TheoryData<string, string> DistinctTenantPairs()
	{
		var data = new TheoryData<string, string>();
		foreach (var (first, second) in AdversarialPairs)
		{
			data.Add(first, second);
		}

		return data;
	}

	[Theory]
	[MemberData(nameof(DistinctTenantPairs))]
	public void MapDistinctTenantsToDistinctSegments(string firstTenantId, string secondTenantId)
	{
		// Arrange — two genuinely different tenants.
		firstTenantId.ShouldNotBe(secondTenantId, "the corpus must only contain DISTINCT tenants");

		var first = KeyedTenantPartition.Scoped(firstTenantId);
		var second = KeyedTenantPartition.Scoped(secondTenantId);

		// Act
		var firstSegment = ColdStorageKey.TenantSegment(first);
		var secondSegment = ColdStorageKey.TenantSegment(second);

		// Assert — SAFETY: no two tenants may share a cold-storage namespace.
		firstSegment.ShouldNotBe(
			secondSegment,
			$"tenants '{firstTenantId}' and '{secondTenantId}' are distinct, so their cold-storage segments "
			+ "must differ; a shared segment lets one tenant's archive satisfy the other's watermark and "
			+ "delete hot events that were never archived");
	}

	/// <summary>
	/// LIVENESS twin. Injectivity alone is satisfied by a segment that is unusable — one that is empty,
	/// or that varies per call so nothing can ever be read back. A tenant must get a stable, non-empty
	/// segment, or the archive is safe by never storing anything retrievable.
	/// </summary>
	[Theory]
	[InlineData("tenant-a")]
	[InlineData("a/b")]
	[InlineData("1!x")]
	public void ProduceAStableNonEmptySegmentForTheSameTenant(string tenantId)
	{
		var tenant = KeyedTenantPartition.Scoped(tenantId);

		var first = ColdStorageKey.TenantSegment(tenant);
		var second = ColdStorageKey.TenantSegment(KeyedTenantPartition.Scoped(tenantId));

		first.ShouldNotBeNullOrWhiteSpace("an empty segment would place every tenant at the same root");
		second.ShouldBe(first, "the segment must be deterministic, or an archived object can never be read back");
	}

	/// <summary>
	/// LIVENESS: the tenant must be RECOVERABLE from the segment — the segment has to carry the tenant, not
	/// merely be unique to it. A one-way hash would satisfy injectivity perfectly and leave an operator
	/// unable to attribute a stored object to its owner.
	/// </summary>
	/// <remarks>
	/// This asserts <i>decodability</i>, not the presence of the literal text. An earlier draft asserted the
	/// segment CONTAINS the tenant id, which would have failed any encoding — i.e. it would have rejected a
	/// correct injective fix for not being plaintext. That is the mechanism-vs-property error: assert what
	/// must be TRUE (the tenant can be recovered), never HOW it is achieved.
	/// </remarks>
	[Theory]
	[InlineData("tenant-a")]
	[InlineData("a/b")]
	[InlineData(@"a\b")]
	[InlineData("1!x")]
	public void ProduceASegmentTheTenantCanBeRecoveredFrom(string tenantId)
	{
		var segment = ColdStorageKey.TenantSegment(KeyedTenantPartition.Scoped(tenantId));

		var recovered = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segment));

		recovered.ShouldBe(
			tenantId,
			"an operator must be able to attribute a stored object to its owning tenant; a one-way "
			+ "transform would be injective and unattributable");
	}

	/// <summary>
	/// NON-VACUITY PROOF. Asserts that this class's corpus actually discriminates — that it REJECTS the
	/// implementation the current one replaced.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A green lock proves nothing until you know it can go red. The usual proof is to mutate the
	/// implementation and watch the test fail — but that file is owned by another agent and being edited
	/// right now, so mutating it would be a collision, and a mutate-restore can strand a mutant or destroy
	/// uncommitted work.
	/// </para>
	/// <para>
	/// Instead the superseded algorithm is reproduced here verbatim and run against the same corpus. If this
	/// test ever fails, the corpus has stopped discriminating and every other assertion in this class is
	/// suspect — it would mean the adversarial pairs no longer collide even under a known-lossy scheme.
	/// </para>
	/// </remarks>
	[Fact]
	public void RejectTheSupersededSanitizingImplementation()
	{
		// The superseded implementation: length-prefix (sound) followed by separator substitution (lossy).
		static string Superseded(string tenantId) =>
			string.Concat(
				tenantId.Length.ToString(CultureInfo.InvariantCulture),
				"!",
				tenantId.Replace('\\', '_').Replace('/', '_'));

		var collisions = AdversarialPairs
			.Where(pair => Superseded(pair.First) == Superseded(pair.Second))
			.ToList();

		collisions.ShouldNotBeEmpty(
			"the corpus must contain at least one pair that the superseded implementation maps to a single "
			+ "segment; if it does not, this lock cannot detect the defect it was written for");
	}

	/// <summary>
	/// SAFETY: the segment must not be able to inject a path boundary into the composed key. A tenant that
	/// can emit '/' or '\' can escape its own prefix in a provider whose key grammar is path-shaped.
	/// </summary>
	[Theory]
	[InlineData("a/b")]
	[InlineData(@"a\b")]
	[InlineData("../../etc")]
	[InlineData("a/../b")]
	public void NeverEmitAPathSeparator(string tenantId)
	{
		var segment = ColdStorageKey.TenantSegment(KeyedTenantPartition.Scoped(tenantId));

		segment.ShouldNotContain("/", Case.Sensitive, "a '/' lets a tenant escape its own key prefix");
		segment.ShouldNotContain(@"\", Case.Sensitive, "a '\\' lets a tenant escape its own key prefix");
	}
}
