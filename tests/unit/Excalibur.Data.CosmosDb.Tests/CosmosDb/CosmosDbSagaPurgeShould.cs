// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.CosmosDb;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit locks for <see cref="CosmosDbSagaStore.PurgeCompletedBeforeAsync"/> and
/// <see cref="CosmosDbSagaStore.PurgeAllTenantsCompletedBeforeAsync"/> (bead qt5kh7, vtklu8).
/// </summary>
/// <remarks>
/// <para>
/// Reference: mirrors the cross-provider purge contract asserted behaviorally by the shared
/// <c>SagaStoreConformanceTestBase</c> purge test (d0wpug/w8aqq3). The Cosmos store exposes a
/// <see cref="CosmosClient"/> seam, so the store is driven through a faked client chain
/// (client → database → container → query iterator) — the purge is exercised end-to-end
/// (query → delete-by-key → count) rather than only source-asserted.
/// </para>
/// <para>
/// Contract: <c>PurgeCompletedBeforeAsync(threshold, ct)</c> deletes ONLY sagas that are completed
/// (<c>completedAt</c> defined and non-null) AND aged (<c>completedAt &lt; threshold</c>) AND owned by the
/// CALLING tenant, NEVER a running saga (<c>completedAt</c> absent/null) and NEVER another tenant's
/// completed sagas, and returns the deleted count. It never refuses: <see cref="TenantScope.TenantId"/> is
/// total, so untenanted, the single-tenant default, and a real tenant all bind a concrete predicate value.
/// <c>PurgeAllTenantsCompletedBeforeAsync</c> is the one estate-wide sweep that applies no tenant predicate
/// at all, and is reachable only by calling it directly.
/// </para>
/// <para>
/// NON-VACUITY: the running-saga exclusion is enforced server-side by the
/// <c>IS_DEFINED(c.completedAt) AND c.completedAt != null</c> guard in the query text; the not-yet-aged
/// exclusion by the <c>c.completedAt &lt; @cutoff</c> bound; the cross-tenant exclusion by the
/// <c>c.tenantId = @tenantId</c> predicate, bound to the CURRENT tenant scope, not the caller's payload. A
/// mutant that drops the IS_DEFINED/non-null guard (running sagas become eligible), the age bound (purges
/// everything), or the tenant predicate (a scoped call reverts to deleting every tenant's rows) changes the
/// captured <see cref="QueryDefinition"/> and turns the query assertion RED. The behavioral test
/// additionally fails if the impl fails to delete a matched document or mis-counts.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
public sealed class CosmosDbSagaPurgeShould
{
	private static CosmosDbSagaStore CreateStore(CosmosClient client, Container container, ITenantContext? tenantContext = null)
	{
		var database = A.Fake<Database>();
		_ = A.CallTo(() => client.GetDatabase(A<string>._)).Returns(database);
		_ = A.CallTo(() => database.GetContainer(A<string>._)).Returns(container);

		var options = Options.Create(new CosmosDbSagaOptions
		{
			DatabaseName = "excalibur",
			ContainerName = "sagas",
			CreateContainerIfNotExists = false,
			Client = new CosmosDbClientOptions { ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==;" },
		});
		return new CosmosDbSagaStore(
			client,
			options,
			A.Fake<ILogger<CosmosDbSagaStore>>(),
			new DispatchJsonSerializer(),
			tenantContext: tenantContext ?? TestTenantContext.Untenanted);
	}

	/// <summary>A context pinned to a real, named tenant, for the scoped-purge tenant-predicate locks.</summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}

	// FeedResponse<T>/FeedIterator<T> are not proxy-fakeable (no accessible ctor for Castle), so use
	// concrete single-page test doubles.
	private static FeedIterator<CosmosDbSagaDocument> SinglePageIterator(IReadOnlyList<CosmosDbSagaDocument> docs)
		=> new TestFeedIterator(new TestFeedResponse(docs));

	private sealed class TestFeedIterator(FeedResponse<CosmosDbSagaDocument> page) : FeedIterator<CosmosDbSagaDocument>
	{
		private bool _served;

		public override bool HasMoreResults => !_served;

		public override Task<FeedResponse<CosmosDbSagaDocument>> ReadNextAsync(CancellationToken cancellationToken = default)
		{
			_served = true;
			return Task.FromResult(page);
		}
	}

	private sealed class TestFeedResponse(IReadOnlyList<CosmosDbSagaDocument> docs) : FeedResponse<CosmosDbSagaDocument>
	{
		public override string? ContinuationToken => null;
		public override int Count => docs.Count;
		public override string? IndexMetrics => null;
		public override Headers Headers => new();
		public override IEnumerable<CosmosDbSagaDocument> Resource => docs;
		public override System.Net.HttpStatusCode StatusCode => System.Net.HttpStatusCode.OK;
		public override CosmosDiagnostics Diagnostics => null!;
		public override double RequestCharge => 0;
		public override IEnumerator<CosmosDbSagaDocument> GetEnumerator() => docs.GetEnumerator();
	}

	[Fact]
	public async Task Purge_QueriesWithCompletedDefinedNonNullAndAgeGuard()
	{
		// Arrange — capture the QueryDefinition; return an empty page.
		QueryDefinition? captured = null;
		var container = A.Fake<Container>();
		_ = A.CallTo(() => container.GetItemQueryIterator<CosmosDbSagaDocument>(
				A<QueryDefinition>._, A<string>._, A<QueryRequestOptions>._))
			.ReturnsLazily((QueryDefinition q, string _, QueryRequestOptions _) =>
			{
				captured = q;
				return SinglePageIterator([]);
			});

		var store = CreateStore(A.Fake<CosmosClient>(), container);
		var threshold = new DateTimeOffset(2026, 07, 04, 12, 00, 00, TimeSpan.Zero);

		// Act
		var removed = await store.PurgeCompletedBeforeAsync(threshold, CancellationToken.None);

		// Assert — the load-bearing running-saga guard lives in the query text.
		removed.ShouldBe(0);
		captured.ShouldNotBeNull();
		captured!.QueryText.ShouldContain("IS_DEFINED(c.completedAt)");
		captured.QueryText.ShouldContain("c.completedAt != null");
		captured.QueryText.ShouldContain("c.completedAt < @cutoff");
	}

	[Theory]
	[InlineData("tenant-a")]
	public async Task Purge_AppliesTheCurrentTenantAsAServerSidePredicate_ForARealTenant(string tenantId)
	{
		// RED against the pre-fix code: a store scoped to a real, named tenant used to REFUSE this call
		// with TenantScopeNotSupportedException. It must now filter instead.
		QueryDefinition? captured = null;
		var container = A.Fake<Container>();
		_ = A.CallTo(() => container.GetItemQueryIterator<CosmosDbSagaDocument>(
				A<QueryDefinition>._, A<string>._, A<QueryRequestOptions>._))
			.ReturnsLazily((QueryDefinition q, string _, QueryRequestOptions _) =>
			{
				captured = q;
				return SinglePageIterator([]);
			});

		var store = CreateStore(A.Fake<CosmosClient>(), container, new FixedTenantContext(tenantId));

		var removed = await store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		removed.ShouldBe(0);
		captured.ShouldNotBeNull();
		captured!.QueryText.ShouldContain("AND c.tenantId = @tenantId");
	}

	[Fact]
	public async Task PurgeAllTenantsCompletedBeforeAsync_AppliesNoTenantPredicate()
	{
		// The estate-wide sweep, called directly, applies no tenant term even when the ambient scope names
		// a real tenant -- distinguishing it from the scoped purge above.
		QueryDefinition? captured = null;
		var container = A.Fake<Container>();
		_ = A.CallTo(() => container.GetItemQueryIterator<CosmosDbSagaDocument>(
				A<QueryDefinition>._, A<string>._, A<QueryRequestOptions>._))
			.ReturnsLazily((QueryDefinition q, string _, QueryRequestOptions _) =>
			{
				captured = q;
				return SinglePageIterator([]);
			});

		var store = CreateStore(A.Fake<CosmosClient>(), container, new FixedTenantContext("tenant-a"));

		_ = await store.PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured!.QueryText.ShouldNotContain("tenantId");
	}

	[Fact]
	public async Task Purge_DeletesMatchedCompletedAgedDocument_AndReturnsCount()
	{
		// Arrange — the server (honoring the guard) returns exactly one eligible document.
		var eligible = new CosmosDbSagaDocument { Id = "saga-old", SagaType = "TestSagaState" };
		var container = A.Fake<Container>();
		_ = A.CallTo(() => container.GetItemQueryIterator<CosmosDbSagaDocument>(
				A<QueryDefinition>._, A<string>._, A<QueryRequestOptions>._))
			.Returns(SinglePageIterator([eligible]));
		_ = A.CallTo(() => container.DeleteItemAsync<CosmosDbSagaDocument>(
				A<string>._, A<PartitionKey>._, A<ItemRequestOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ItemResponse<CosmosDbSagaDocument>>(null!));

		var store = CreateStore(A.Fake<CosmosClient>(), container);

		// Act
		var removed = await store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert — the matched document is deleted by (id, partition key) and counted.
		removed.ShouldBe(1);
		A.CallTo(() => container.DeleteItemAsync<CosmosDbSagaDocument>(
				"saga-old", new PartitionKey("TestSagaState"), A<ItemRequestOptions>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Purge_EmptyResult_RemovesNothing()
	{
		var container = A.Fake<Container>();
		_ = A.CallTo(() => container.GetItemQueryIterator<CosmosDbSagaDocument>(
				A<QueryDefinition>._, A<string>._, A<QueryRequestOptions>._))
			.Returns(SinglePageIterator([]));

		var store = CreateStore(A.Fake<CosmosClient>(), container);

		var removed = await store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		removed.ShouldBe(0);
		A.CallTo(() => container.DeleteItemAsync<CosmosDbSagaDocument>(
				A<string>._, A<PartitionKey>._, A<ItemRequestOptions>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}
