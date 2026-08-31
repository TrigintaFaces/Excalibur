// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.MaterializedViews;

using Shouldly;

using Xunit;

namespace Excalibur.Data.Tests.MongoDB.MaterializedViews;

/// <summary>
/// Binds the composition rule for the document providers' identifiers: the tenant segment must be
/// self-delimiting, so that no two distinct tenants can address the same document.
/// </summary>
/// <remarks>
/// <para>
/// The SQL providers get their partition from a primary key the engine enforces, which is why their locks
/// are integration tests. The document providers have no such engine-side constraint: the partition IS the
/// identifier, so its composition is the whole mechanism and it is provable in-process. That is what this
/// file covers, and it is the reason it is a unit test rather than a container test.
/// </para>
/// <para>
/// <b>The defect this rules out is subtle and would otherwise be invisible.</b> A tenant identifier may
/// legally contain the delimiter. Composing <c>{tenant}:{viewName}:{viewId}</c> then lets ("a", "b", "c")
/// and ("a:b", "c", …) collapse onto one identifier — a cross-tenant collision reintroduced by the very
/// code written to prevent one, and one that would be found by a customer rather than by a test, because
/// it needs a tenant id nobody thinks to try. The length prefix makes the segment self-delimiting.
/// </para>
/// <para>
/// <b>This is stated against MongoDB's factory because it is the one that is public.</b> The Elasticsearch
/// and OpenSearch stores compose their identifiers with the identical rule through a private helper, so
/// this file is evidence for the rule and for MongoDB's implementation of it — <b>not</b> subsystem
/// coverage of the other two providers. Read it that way.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class MaterializedViewDocumentIdTenantPartitioningShould
{
	private const string ViewName = "OrderSummary";
	private const string ViewId = "order-1";

	/// <summary>
	/// SAFETY: two tenants projecting the same named view must never address the same document.
	/// </summary>
	[Fact]
	public void GiveTwoTenantsDistinctViewDocumentIds()
	{
		var forTenantA = MongoDbMaterializedViewDocument.CreateId("tenant-a", ViewName, ViewId);
		var forTenantB = MongoDbMaterializedViewDocument.CreateId("tenant-b", ViewName, ViewId);

		forTenantA.ShouldNotBe(
			forTenantB,
			"two tenants sharing a view document identifier share the document: the later writer's data "
			+ "silently replaces the earlier one's and a read returns whichever tenant wrote last");
	}

	/// <summary>
	/// SAFETY: the same, for the checkpoint document — the arm whose failure is data loss rather than
	/// disclosure, because a shared checkpoint makes one tenant's projector skip events permanently.
	/// </summary>
	[Fact]
	public void GiveTwoTenantsDistinctCheckpointDocumentIds()
	{
		var forTenantA = MongoDbMaterializedViewDocument.CreatePositionId("tenant-a", ViewName);
		var forTenantB = MongoDbMaterializedViewDocument.CreatePositionId("tenant-b", ViewName);

		forTenantA.ShouldNotBe(
			forTenantB,
			"a shared checkpoint lets one tenant's progress advance another's, so that tenant's projector "
			+ "skips every event in between with no error raised");
	}

	/// <summary>
	/// SAFETY: a tenant identifier containing the delimiter must not be able to compose onto another
	/// tenant's identifier. This is the arm the length prefix exists for; without it these two are equal.
	/// </summary>
	[Fact]
	public void NotLetADelimiterInsideATenantIdForgeAnotherTenantsDocumentId()
	{
		// Chosen so that a bare "{tenant}:{viewName}:{viewId}" composition produces "a:b:c:order-1" for
		// BOTH — the collision the prefix removes.
		var honest = MongoDbMaterializedViewDocument.CreateId("a", "b:c", ViewId);
		var forged = MongoDbMaterializedViewDocument.CreateId("a:b", "c", ViewId);

		honest.ShouldNotBe(
			forged,
			"a tenant identifier may legally contain the delimiter; without a self-delimiting tenant segment "
			+ "one tenant can address another's document by choosing its name");
	}

	/// <summary>
	/// The same forgery, on the checkpoint identifier.
	/// </summary>
	[Fact]
	public void NotLetADelimiterInsideATenantIdForgeAnotherTenantsCheckpointId()
	{
		var honest = MongoDbMaterializedViewDocument.CreatePositionId("a", "b:c");
		var forged = MongoDbMaterializedViewDocument.CreatePositionId("a:b", "c");

		honest.ShouldNotBe(forged, "the checkpoint identifier needs the same self-delimiting tenant segment");
	}

	/// <summary>
	/// LIVENESS: the identifier is still a deterministic function of its inputs. Isolation achieved by
	/// returning something different every time would satisfy every arm above and make the store unusable —
	/// a save and the read that follows it would never address the same document.
	/// </summary>
	[Fact]
	public void ReturnTheSameIdentifierForTheSameTenantAndView()
	{
		MongoDbMaterializedViewDocument.CreateId("tenant-a", ViewName, ViewId)
			.ShouldBe(
				MongoDbMaterializedViewDocument.CreateId("tenant-a", ViewName, ViewId),
				"a save and the read that follows it must address the same document");

		MongoDbMaterializedViewDocument.CreatePositionId("tenant-a", ViewName)
			.ShouldBe(
				MongoDbMaterializedViewDocument.CreatePositionId("tenant-a", ViewName),
				"a checkpoint write and the read that follows it must address the same document");
	}

	/// <summary>
	/// LIVENESS: distinct views within one tenant still get distinct identifiers. A fix that keyed on the
	/// tenant alone would pass every safety arm above while collapsing all of a tenant's views into one
	/// document — and all of its projections onto one checkpoint.
	/// </summary>
	[Fact]
	public void StillSeparateDistinctViewsWithinOneTenant()
	{
		MongoDbMaterializedViewDocument.CreateId("tenant-a", "OrderSummary", ViewId)
			.ShouldNotBe(
				MongoDbMaterializedViewDocument.CreateId("tenant-a", "CustomerHistory", ViewId),
				"partitioning by tenant must not collapse a tenant's distinct views onto one document");

		MongoDbMaterializedViewDocument.CreateId("tenant-a", ViewName, "order-1")
			.ShouldNotBe(
				MongoDbMaterializedViewDocument.CreateId("tenant-a", ViewName, "order-2"),
				"partitioning by tenant must not collapse a view's distinct instances onto one document");

		MongoDbMaterializedViewDocument.CreatePositionId("tenant-a", "OrderSummary")
			.ShouldNotBe(
				MongoDbMaterializedViewDocument.CreatePositionId("tenant-a", "CustomerHistory"),
				"partitioning by tenant must not collapse a tenant's projections onto one checkpoint");
	}
}
