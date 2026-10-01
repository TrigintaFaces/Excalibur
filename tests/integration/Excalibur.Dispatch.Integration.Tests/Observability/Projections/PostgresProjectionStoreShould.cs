// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor — field is set in InitializeAsync()

using Dapper;

using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;

using Npgsql;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Integration tests for <see cref="PostgresProjectionStore{TProjection}"/>.
/// Tests all IProjectionStore operations including CRUD, filtering, pagination, and sorting.
/// </summary>
[Collection("Postgres Projection Store Tests")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class PostgresProjectionStoreShould : IClassFixture<PostgresFixture>, IAsyncLifetime
{
	private const string TableName = "test_order_projection";

	/// <summary>The tenant every projection in this suite is written under and read back through.</summary>
	private const string ProjectionTestTenantId = "tenant-projection-tests";

	/// <summary>
	/// A fixed, always-resolved <see cref="ITenantContext"/>. The production default reads the ambient
	/// holder, but that implementation is internal to Excalibur.Dispatch, so a directly-constructed store
	/// needs an explicit context supplied here.
	/// </summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
	private readonly PostgresFixture _fixture;
	private readonly ILogger<PostgresProjectionStore<TestOrderProjection>> _logger;
	private PostgresProjectionStore<TestOrderProjection> _store;

	public PostgresProjectionStoreShould(PostgresFixture fixture)
	{
		_fixture = fixture;
		_logger = new LoggerFactory().CreateLogger<PostgresProjectionStore<TestOrderProjection>>();
	}

	public async ValueTask InitializeAsync()
	{
		// Skip if Docker is not available
		// EnsureAvailable() THROWS, where this used to `return`. An early return in a [Fact] is an
		// empty test that genuinely ran -- counted in executed AND passed, indistinguishable from
		// real work by any counter. The fixture owns the policy and it is hard failure.
		_fixture.EnsureAvailable();

		// Create the projection table with Postgres schema
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync();

		_ = await connection.ExecuteAsync($"""
			CREATE TABLE IF NOT EXISTS "{TableName}" (
				id VARCHAR(450) NOT NULL,
				tenant_id VARCHAR(255) NOT NULL,
				data JSONB NOT NULL,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL,
				-- The store's unconditional write now INVALIDATES the position rather than leaving it
				-- stale, so this column is part of the contract the store writes against, not an
				-- optional extra.
				--
				-- -2 IS THE DOCUMENTED DEFAULT AND THE VALUE MATTERS. It is the sentinel meaning "this
				-- row's position was never established", which is what a defaulted column records:
				-- nobody said anything about this row. Not 0, because zero is a legitimate stream
				-- position and a zero default would make a brand-new row claim it had already folded the
				-- first event. And NOT -1, which is the sentinel meaning "a caller asserted this state is
				-- a complete fold" -- putting that assertion on every row a consumer inserted without the
				-- column, and on every row of a table altered to add it.
				last_applied_position BIGINT NOT NULL DEFAULT -2,
				-- Composite (id, tenant_id), NOT id alone. Two tenants may legitimately hold the same
				-- projection id; keying on id alone would let one tenant's upsert overwrite another's row.
				-- PostgresProjectionStore states this requirement directly at PostgresProjectionStore.cs:177.
				PRIMARY KEY (id, tenant_id)
			)
			""");

		// A tenant context is REQUIRED, not optional decoration. RequireTenant is enabled, so a store
		// built without one resolves no tenant and every call fails closed with TenantRequiredException
		// -- which is the store behaving CORRECTLY. The defect was this test asserting projection
		// behaviour while supplying no tenant at all, so it never exercised a tenanted path.
		_store = new PostgresProjectionStore<TestOrderProjection>(
			_fixture.ConnectionString,
			_logger,
			tenantContext: new FixedTenantContext(ProjectionTestTenantId),
			tableName: TableName,
			jsonOptions: null);
	}

	// SAFETY. A row whose position an unconditional write DESTROYED must be distinguishable, in the
	// stored value itself, from a row that simply never had one. Neither can be advanced from -- a
	// positioned write has no number to match in either -- so the distinction does not decide the next
	// write; it decides what the row can honestly be said to hold while it waits to be rebuilt, which is
	// what an operator reads it for. Nothing can tell them apart after the fact, so the distinction has
	// to be recorded AT WRITE TIME or it is lost for good.
	[Fact]
	public async Task Record_that_an_unconditional_write_left_the_state_unplaceable()
	{
		var id = $"marker-{Guid.NewGuid():N}";

		var positioned = (IPositionedProjectionStore<TestOrderProjection>)
			((IServiceProvider)_store!).GetService(
				typeof(IPositionedProjectionStore<TestOrderProjection>))!;

		_ = await positioned.UpsertAtPositionAsync(
			id, new TestOrderProjection { Id = id }, expectedPosition: null, newPosition: 7,
			CancellationToken.None);

		(await ReadPositionAsync(id)).ShouldBe(7, "precondition: the row is positioned");

		// The unconditional surface replaces the state with something not folded from any known prefix.
		await _store.UpsertAsync(id, new TestOrderProjection { Id = id }, CancellationToken.None);

		(await ReadPositionAsync(id)).ShouldBe(
			ProjectionPosition.UnplaceableSentinel,
			"the row must say the state is UNPLACEABLE -- not a fold over any prefix -- rather than "
			+ "merely unnumbered, which is what the old single sentinel could not express");

		// And the refusal must be visible to a positioned writer, not merely recorded in the row.
		var refused = await positioned.UpsertAtPositionAsync(
			id, new TestOrderProjection { Id = id }, expectedPosition: null, newPosition: 9,
			CancellationToken.None);

		refused.Outcome.ShouldBe(
			ProjectionAdvanceOutcome.Unplaceable,
			"adopting this row would make it assert a prefix it does not hold. Reporting Superseded "
			+ "instead would send the caller back to re-read and retry, forever");
	}

	// LIVENESS, and it is the arm that stops the one above being satisfied by refusing everything. The
	// liveness is NOT adoption -- a positioned write refuses every row carrying no number, whichever of
	// the two no-number states it holds. It is that the refusal is REPAIRABLE: a whole-stream rebuild
	// writes the state and its position together and needs no prior prefix to be conditional on, which is
	// what keeps a loud refusal from being a permanent stall.
	[Fact]
	public async Task Refuse_an_unnumbered_complete_fold_and_repair_it_with_a_rebuild()
	{
		var id = $"marker-new-{Guid.NewGuid():N}";

		var positioned = (IPositionedProjectionStore<TestOrderProjection>)
			((IServiceProvider)_store!).GetService(
				typeof(IPositionedProjectionStore<TestOrderProjection>))!;

		await positioned.UpsertUnnumberedAsync(
			id, new TestOrderProjection { Id = id }, CancellationToken.None);

		(await ReadPositionAsync(id)).ShouldBe(
			ProjectionPosition.UnnumberedSentinel,
			"a complete fold with no position NUMBER is unnumbered, not unplaceable");

		var refused = await positioned.UpsertAtPositionAsync(
			id, new TestOrderProjection { Id = id }, expectedPosition: null, newPosition: 4,
			CancellationToken.None);

		refused.Outcome.ShouldBe(
			ProjectionAdvanceOutcome.Unplaceable,
			"a caller that read no position knows nothing about which prefix this state covers, so stamping "
			+ "its batch's position onto it would make the row assert a prefix the state need not hold. The "
			+ "outcome reports only that there was no number to advance from, which is what the write "
			+ "established; Superseded is the answer that must not appear, because it sends the caller back "
			+ "to re-read and retry against a row that never changes");

		(await ReadPositionAsync(id)).ShouldBe(
			ProjectionPosition.UnnumberedSentinel,
			"the refused write must not have moved the position");

		var repaired = await positioned.RebuildAtPositionAsync(
			id, new TestOrderProjection { Id = id }, newPosition: 4, CancellationToken.None);

		repaired.Outcome.ShouldBe(
			ProjectionRebuildOutcome.Applied,
			"the refusal above is legitimate only because the row has an exit. Without this, refusing a "
			+ "numberless row is a silent permanent stall: the caller reads no position, claims none, and is "
			+ "refused identically forever");

		(await ReadPositionAsync(id)).ShouldBe(
			4L,
			"the repaired row must carry the number the rebuild wrote, or it is still unadvanceable and "
			+ "nothing was repaired");
	}


	// SAFETY. A row created by the DOCUMENTED DDL -- position column left to its default, never written by
	// any positioned or unnumbered write -- must read back as UNPLACEABLE and be refused.
	//
	// THIS IS THE ARM WHOSE ABSENCE LET A DOC OBLIGATION AND A DECODER DISAGREE. The guidance used to
	// prescribe DEFAULT -1, which is the sentinel meaning "a caller asserted this state is a complete
	// fold". Nobody asserted anything about a defaulted row, and the largest population that value will
	// ever hold is every row of a table altered to add the column -- so the decoder reported an assertion
	// manufactured by a DDL default. Nothing exercised the documented creation path, so the two could not
	// contradict each other anywhere a test could see.
	//
	// The INSERT omits the column deliberately. Writing it explicitly would test this suite's opinion of
	// the default rather than the default itself.
	[Fact]
	public async Task Refuse_a_row_whose_position_column_was_left_to_the_documented_default()
	{
		var id = $"defaulted-{Guid.NewGuid():N}";

		await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			_ = await connection.ExecuteAsync(
				$"INSERT INTO \"{TableName}\" (tenant_id, id, data, created_at, updated_at) "
				+ "VALUES (@TenantId, @Id, @Data::jsonb, @Now, @Now)",
				new
				{
					TenantId = ProjectionTestTenantId,
					Id = id,
					Data = "{\"Id\":\"" + id + "\"}",
					Now = DateTimeOffset.UtcNow,
				});
		}

		(await ReadPositionAsync(id)).ShouldBe(
			ProjectionPosition.UnplaceableSentinel,
			"the documented default must be the sentinel meaning the position was never established. A -1 "
			+ "default would store the sentinel meaning a caller ASSERTED a complete fold, about a row "
			+ "nobody asserted anything about");

		var positioned = (IPositionedProjectionStore<TestOrderProjection>)
			((IServiceProvider)_store!).GetService(
				typeof(IPositionedProjectionStore<TestOrderProjection>))!;

		var (_, read) = await positioned.GetWithPositionAsync(id, CancellationToken.None);

		read.Kind.ShouldBe(
			ProjectionPositionKind.Unplaceable,
			"a defaulted row is not a fold over any prefix, so the read must not report it as a complete "
			+ "fold awaiting a coordinate");

		var refused = await positioned.UpsertAtPositionAsync(
			id, new TestOrderProjection { Id = id }, expectedPosition: null, newPosition: 5,
			CancellationToken.None);

		refused.Outcome.ShouldBe(
			ProjectionAdvanceOutcome.Unplaceable,
			"a positioned write has no number to advance from, so the row is refused terminally and must "
			+ "be rebuilt -- which is exactly what the guarantee document promises for a projection that "
			+ "predates the column");
	}
	private async Task<long> ReadPositionAsync(string id)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync();

		return await connection.ExecuteScalarAsync<long>(
			$"""SELECT last_applied_position FROM "{TableName}" WHERE id = @Id AND tenant_id = @TenantId""",
			new { Id = id, TenantId = ProjectionTestTenantId });
	}

	public async ValueTask DisposeAsync()
	{
		// EnsureAvailable() THROWS, where this used to `return`. An early return in a [Fact] is an
		// empty test that genuinely ran -- counted in executed AND passed, indistinguishable from
		// real work by any counter. The fixture owns the policy and it is hard failure.
		_fixture.EnsureAvailable();

		// Clean up test data
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync();
		_ = await connection.ExecuteAsync($"DELETE FROM \"{TableName}\"");
	}

	#region CRUD Tests (5)

	[Fact]
	public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid().ToString();

		// Act
		var result = await _store.GetByIdAsync(nonExistentId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task UpsertAsync_CreatesNewProjection()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		var projection = TestOrderProjection.Create(
			id,
			"customer-1",
			"Active",
			100.50m,
			5,
			DateTimeOffset.UtcNow,
			["important"],
			"Widget");

		// Act
		await _store.UpsertAsync(id, projection, CancellationToken.None);
		var result = await _store.GetByIdAsync(id, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(id);
		result.CustomerId.ShouldBe("customer-1");
		result.Status.ShouldBe("Active");
		result.Amount.ShouldBe(100.50m);
		result.Quantity.ShouldBe(5);
		result.ProductName.ShouldBe("Widget");
	}

	[Fact]
	public async Task UpsertAsync_UpdatesExistingProjection()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		var original = TestOrderProjection.Create(id, "customer-1", "Pending", 50m);
		await _store.UpsertAsync(id, original, CancellationToken.None);

		var updated = TestOrderProjection.Create(id, "customer-1", "Shipped", 75m);

		// Act
		await _store.UpsertAsync(id, updated, CancellationToken.None);
		var result = await _store.GetByIdAsync(id, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe("Shipped");
		result.Amount.ShouldBe(75m);
	}

	[Fact]
	public async Task DeleteAsync_RemovesProjection()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		var projection = TestOrderProjection.Create(id, "customer-1");
		await _store.UpsertAsync(id, projection, CancellationToken.None);

		// Verify it exists
		var beforeDelete = await _store.GetByIdAsync(id, CancellationToken.None);
		_ = beforeDelete.ShouldNotBeNull();

		// Act
		await _store.DeleteAsync(id, CancellationToken.None);
		var result = await _store.GetByIdAsync(id, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteAsync_Succeeds_WhenNotExists()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid().ToString();

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(async () =>
			await _store.DeleteAsync(nonExistentId, CancellationToken.None));
	}

	#endregion CRUD Tests (5)

	#region Equality Filter Tests (2)

	[Fact]
	public async Task QueryAsync_FiltersBy_Equality()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", "Active"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", "Pending"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", "Active"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status"] = "Active" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Status == "Active");
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_NotEquals()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", "Active"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", "Deleted"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", "Pending"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status:neq"] = "Deleted" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Status != "Deleted");
	}

	#endregion Equality Filter Tests (2)

	#region Comparison Filter Tests (4)

	[Fact]
	public async Task QueryAsync_FiltersBy_GreaterThan()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", amount: 50m), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", amount: 100m), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", amount: 150m), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Amount:gt"] = 75 };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Amount > 75);
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_GreaterThanOrEqual()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", amount: 50m), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", amount: 100m), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", amount: 150m), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Amount:gte"] = 100 };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Amount >= 100);
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_LessThan()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", quantity: 5), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", quantity: 10), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", quantity: 15), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Quantity:lt"] = 12 };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Quantity < 12);
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_LessThanOrEqual()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", quantity: 5), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", quantity: 10), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", quantity: 15), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Quantity:lte"] = 10 };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Quantity <= 10);
	}

	#endregion Comparison Filter Tests (4)

	#region Collection/String Filter Tests (2)

	[Fact]
	public async Task QueryAsync_FiltersBy_In()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", "Active"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", "Shipped"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", "Pending"), CancellationToken.None);

		var filters = new Dictionary<string, object>
		{
			["Status:in"] = new[] { "Active", "Shipped" }
		};

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Status == "Active" || p.Status == "Shipped");
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_Contains()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", productName: "Test Widget"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", productName: "Another Gadget"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", productName: "Widget Pro"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["ProductName:contains"] = "Widget" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.ProductName.Contains("Widget"));
	}

	#endregion Collection/String Filter Tests (2)

	#region Pagination Tests (3)

	[Fact]
	public async Task QueryAsync_Pagination_Skip()
	{
		// Arrange - Create 5 projections
		for (var i = 1; i <= 5; i++)
		{
			var id = $"skip-test-{i:D3}";
			await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);
		}

		var options = new QueryOptions(Skip: 2);

		// Act
		var results = await _store.QueryAsync(null, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3); // Skipped first 2, got remaining 3
	}

	[Fact]
	public async Task QueryAsync_Pagination_Take()
	{
		// Arrange - Create 5 projections
		for (var i = 1; i <= 5; i++)
		{
			var id = $"take-test-{i:D3}";
			await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);
		}

		var options = new QueryOptions(Take: 3);

		// Act
		var results = await _store.QueryAsync(null, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3);
	}

	[Fact]
	public async Task QueryAsync_Pagination_SkipAndTake()
	{
		// Arrange - Create 10 projections
		for (var i = 1; i <= 10; i++)
		{
			var id = $"page-test-{i:D3}";
			await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);
		}

		var options = new QueryOptions(Skip: 3, Take: 4);

		// Act
		var results = await _store.QueryAsync(null, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(4);
	}

	#endregion Pagination Tests (3)

	#region Sorting Tests (2)

	[Fact]
	public async Task QueryAsync_OrderBy_Ascending()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", amount: 300m), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", amount: 100m), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", amount: 200m), CancellationToken.None);

		var filters = new Dictionary<string, object>
		{
			["CustomerId:in"] = new[] { "c1", "c2", "c3" }
		};
		var options = new QueryOptions(OrderBy: "Amount", Descending: false);

		// Act
		var results = await _store.QueryAsync(filters, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3);
		results[0].Amount.ShouldBe(100m);
		results[1].Amount.ShouldBe(200m);
		results[2].Amount.ShouldBe(300m);
	}

	[Fact]
	public async Task QueryAsync_OrderBy_Descending()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", amount: 300m), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", amount: 100m), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", amount: 200m), CancellationToken.None);

		var filters = new Dictionary<string, object>
		{
			["CustomerId:in"] = new[] { "c1", "c2", "c3" }
		};
		var options = new QueryOptions(OrderBy: "Amount", Descending: true);

		// Act
		var results = await _store.QueryAsync(filters, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3);
		results[0].Amount.ShouldBe(300m);
		results[1].Amount.ShouldBe(200m);
		results[2].Amount.ShouldBe(100m);
	}

	#endregion Sorting Tests (2)

	#region Count Tests (2)

	[Fact]
	public async Task CountAsync_ReturnsTotal_WhenNoFilters()
	{
		// Arrange - Create 5 projections
		for (var i = 1; i <= 5; i++)
		{
			var id = $"count-total-{i:D3}";
			await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);
		}

		// Act
		var count = await _store.CountAsync(null, CancellationToken.None);

		// Assert
		count.ShouldBeGreaterThanOrEqualTo(5);
	}

	[Fact]
	public async Task CountAsync_ReturnsFiltered_WhenFiltersApplied()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", "Active"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", "Pending"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", "Active"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status"] = "Active" };

		// Act
		var count = await _store.CountAsync(filters, CancellationToken.None);

		// Assert
		count.ShouldBe(2);
	}

	#endregion Count Tests (2)

	#region Edge Case Tests (3)

	[Fact]
	public async Task QueryAsync_ReturnsEmpty_WhenNoMatches()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1", "Active"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status"] = "NonExistentStatus" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryAsync_HandlesNull_Filters()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(null, null, CancellationToken.None);

		// Assert
		results.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task QueryAsync_HandlesNull_Options()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1", "TestStatus"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status"] = "TestStatus" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
	}

	#endregion Edge Case Tests (3)
}

/// <summary>
/// Collection definition for Postgres projection store tests.
/// Ensures tests run sequentially to avoid database conflicts.
/// </summary>
[CollectionDefinition("Postgres Projection Store Tests")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class PostgresProjectionStoreTestCollection : ICollectionFixture<PostgresFixture>;
