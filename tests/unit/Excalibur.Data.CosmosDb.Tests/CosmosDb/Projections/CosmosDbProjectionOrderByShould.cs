// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Projections;
using Excalibur.EventSourcing;

namespace Excalibur.Data.CosmosDb.Tests.Projections;

/// <summary>
/// Locks the pagination-stability tiebreaker on <see cref="CosmosDbProjectionStore{TProjection}"/>'s
/// ORDER BY builder (y6swku): a consumer-supplied, potentially non-unique OrderBy must always be followed
/// by the unique <c>c.id</c> key so page boundaries are deterministic and items are neither skipped nor
/// duplicated between adjacent pages.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CosmosDbProjectionOrderByShould
{
	private sealed class TestProjection
	{
		public string Name { get; set; } = string.Empty;
	}

	private static string BuildOrderBy(QueryOptions? options) =>
		CosmosDbProjectionStore<TestProjection>.BuildOrderByClause(options);

	[Fact]
	public void Default_to_id_when_no_custom_order_by()
	{
		BuildOrderBy(null).ShouldBe(" ORDER BY c.id");
		BuildOrderBy(QueryOptions.Default).ShouldBe(" ORDER BY c.id");
	}

	[Fact]
	public void Append_id_tiebreaker_to_a_custom_ascending_order_by()
	{
		// Non-vacuity: the pre-fix builder returned " ORDER BY c.name ASC" with no secondary key.
		BuildOrderBy(new QueryOptions(OrderBy: "Name")).ShouldBe(" ORDER BY c.name ASC, c.id");
	}

	[Fact]
	public void Append_id_tiebreaker_to_a_custom_descending_order_by()
	{
		BuildOrderBy(new QueryOptions(OrderBy: "Name", Descending: true))
			.ShouldBe(" ORDER BY c.name DESC, c.id");
	}

	[Fact]
	public void Not_duplicate_the_id_key_when_the_custom_order_by_is_already_id()
	{
		BuildOrderBy(new QueryOptions(OrderBy: "Id")).ShouldBe(" ORDER BY c.id ASC");
	}
}
