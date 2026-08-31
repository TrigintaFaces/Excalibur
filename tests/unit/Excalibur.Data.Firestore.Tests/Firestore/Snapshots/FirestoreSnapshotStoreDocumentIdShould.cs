// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Reflection;

using Excalibur.Data.Firestore.Snapshots;
using Excalibur.Dispatch;

namespace Excalibur.Data.Tests.Firestore.Snapshots;

/// <summary>
/// Regression locks on the document id this store derives from (tenant, aggregate type, aggregate id).
/// </summary>
/// <remarks>
/// <para>
/// The property under test is <b>injectivity</b>: distinct triples must produce distinct document ids.
/// It is a safety property about tenant isolation, not a formatting preference. The store keeps one
/// document per aggregate and addresses it by this id alone, so two triples that render the same id are
/// the same document — one tenant reading, and then overwriting, another tenant's snapshot.
/// </para>
/// <para>
/// The segments are joined with <c>_</c>, so the separator itself must be escaped inside a segment or the
/// id stops being injective across the segment boundary: tenant <c>a</c> with type <c>b_c</c> and tenant
/// <c>a_b</c> with type <c>c</c> otherwise both render the same id. <c>%</c> is escaped first and is what
/// makes the whole escaping reversible; <c>/</c> is escaped because Firestore reads it as a path
/// separator rather than as part of an id.
/// </para>
/// <para>
/// The id function is private, and it is reached here by reflection rather than by widening production
/// visibility — the escaping is an implementation detail of the store and must stay one.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class FirestoreSnapshotStoreDocumentIdShould : UnitTestBase
{
	private static readonly MethodInfo CreateDocumentIdMethod =
		typeof(FirestoreSnapshotStore).GetMethod(
			"CreateDocumentId",
			BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException(
			"FirestoreSnapshotStore.CreateDocumentId(string, string) was not found. This lock reaches the "
			+ "id function by reflection; if it was renamed or reshaped, update the lock rather than "
			+ "deleting it — the injectivity property it guards has not gone away.");

	/// <summary>
	/// The exact collision the escaping exists to prevent: the separator appearing inside a segment lets a
	/// tenant boundary be crossed. Both triples name aggregate <c>agg-1</c>, and without escaping both
	/// render the same document id.
	/// </summary>
	[Fact]
	public void SeparatorInsideASegmentDoesNotCollideAcrossTheTenantBoundary()
	{
		var first = CreateDocumentId(tenantId: "a", aggregateType: "b_c", aggregateId: "agg-1");
		var second = CreateDocumentId(tenantId: "a_b", aggregateType: "c", aggregateId: "agg-1");

		first.ShouldNotBe(
			second,
			"tenant 'a' (type 'b_c') and tenant 'a_b' (type 'c') must address different documents. The "
			+ "store keeps one document per aggregate and addresses it by this id alone, so an id shared "
			+ "between two tenants is one tenant reading and overwriting the other's snapshot.");
	}

	/// <summary>
	/// The same crossing one segment further along: the boundary between aggregate type and aggregate id.
	/// </summary>
	[Fact]
	public void SeparatorInsideASegmentDoesNotCollideAcrossTheTypeBoundary()
	{
		var first = CreateDocumentId(tenantId: "t1", aggregateType: "Order_v2", aggregateId: "42");
		var second = CreateDocumentId(tenantId: "t1", aggregateType: "Order", aggregateId: "v2_42");

		first.ShouldNotBe(
			second,
			"an aggregate id may legally contain the separator, so 'Order_v2'/'42' and 'Order'/'v2_42' "
			+ "must remain distinct documents within one tenant.");
	}

	/// <summary>
	/// Injectivity over a corpus whose members differ only in where the metacharacters fall. Every pair
	/// must be distinct; a single duplicate is a cross-aggregate or cross-tenant collision.
	/// </summary>
	/// <remarks>
	/// Stated as a whole-corpus check rather than as a list of pairs so a future escaping change is
	/// measured against every combination at once, not against the cases someone thought to enumerate.
	/// </remarks>
	[Fact]
	public void MapDistinctTriplesToDistinctDocumentIds()
	{
		(string Tenant, string Type, string Id)[] triples =
		[
			("a", "b_c", "agg-1"),
			("a_b", "c", "agg-1"),
			("a", "b", "c_agg-1"),
			("a_b_c", "agg-1", "x"),
			("a", "b_c_agg-1", "x"),
			("t1", "Order_v2", "42"),
			("t1", "Order", "v2_42"),
			("a%5Fb", "c", "agg-1"),
			("a_b", "c", "agg%2D1"),
			("a/b", "c", "agg-1"),
			("a%2Fb", "c", "agg-1"),
			("a%25", "c", "agg-1"),
			("a", "%25c", "agg-1"),
			("a", "b", "c"),
			("t_a", "b", "c"),
		];

		var rendered = triples
			.Select(t => (Triple: t, DocumentId: CreateDocumentId(t.Tenant, t.Type, t.Id)))
			.ToList();

		var collisions = rendered
			.GroupBy(r => r.DocumentId, StringComparer.Ordinal)
			.Where(g => g.Count() > 1)
			.Select(g => string.Format(
				CultureInfo.InvariantCulture,
				"'{0}' <- {1}",
				g.Key,
				string.Join(
					" AND ",
					g.Select(r => $"(tenant '{r.Triple.Tenant}', type '{r.Triple.Type}', id '{r.Triple.Id}')"))))
			.ToList();

		collisions.ShouldBeEmpty(
			"every distinct (tenant, aggregate type, aggregate id) triple must render a distinct document "
			+ "id. Each line below is two triples sharing one document — a snapshot one of them writes is "
			+ "a snapshot the other reads:"
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, collisions));
	}

	/// <summary>
	/// LIVENESS. Injectivity alone is satisfied by an id function that returns a fresh value every call —
	/// which would collide with nothing and also find nothing, because the read would never address the
	/// document the write created. The id must be a pure function of its inputs.
	/// </summary>
	[Fact]
	public void ProduceTheSameDocumentIdForTheSameTripleEveryTime()
	{
		var first = CreateDocumentId(tenantId: "a_b", aggregateType: "Order_v2", aggregateId: "42/7");
		var second = CreateDocumentId(tenantId: "a_b", aggregateType: "Order_v2", aggregateId: "42/7");

		second.ShouldBe(
			first,
			"the id must be a pure function of (tenant, type, id). A non-deterministic id would satisfy "
			+ "injectivity and still be useless: the read would never address the document the write "
			+ "created.");
	}

	/// <summary>
	/// LIVENESS. The id must remain a usable Firestore document id: Firestore reads <c>/</c> as a path
	/// separator, so an unescaped one writes the snapshot somewhere the matching read never looks.
	/// </summary>
	[Fact]
	public void ProduceADocumentIdFirestoreCanAddress()
	{
		var documentId = CreateDocumentId(tenantId: "a/b", aggregateType: "Order/v2", aggregateId: "42/7");

		documentId.ShouldNotBeNullOrWhiteSpace();
		documentId.ShouldNotContain(
			"/",
			Case.Sensitive,
			"a Firestore document id may not contain '/' — it is the path separator, so the snapshot would "
			+ "be written to a nested collection path the matching read never looks at.");
	}

	/// <summary>
	/// LIVENESS for the escaping itself: it must still carry the caller's data through, not flatten it.
	/// A function mapping every segment onto a constant would be injective in no useful sense, and is
	/// rejected here by requiring the ordinary case to survive intact.
	/// </summary>
	[Fact]
	public void CarryOrdinaryTenantTypeAndIdThroughUnaltered()
	{
		var documentId = CreateDocumentId(tenantId: "acme", aggregateType: "Order", aggregateId: "42");

		documentId.ShouldBe(
			"t_acme_Order_42",
			"a triple containing none of the escaped characters must render the plain composite id, so "
			+ "documents already stored under that shape stay addressable.");
	}

	private static string CreateDocumentId(string tenantId, string aggregateType, string aggregateId)
	{
		var store = new FirestoreSnapshotStore(
			Options.Create(new FirestoreSnapshotStoreOptions
			{
				ProjectId = "test-project",
				CollectionName = "snapshots",
			}),
			A.Fake<ILogger<FirestoreSnapshotStore>>(),
			new FixedTenantContext(tenantId));

		try
		{
			return (string)CreateDocumentIdMethod.Invoke(store, [aggregateType, aggregateId])!;
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}

	/// <summary>
	/// An ambient tenant context bound to an arbitrary tenant. The shared test context offers only the two
	/// canonical identities; injectivity has to be exercised across tenant ids chosen to collide, so this
	/// states the tenant directly.
	/// </summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
