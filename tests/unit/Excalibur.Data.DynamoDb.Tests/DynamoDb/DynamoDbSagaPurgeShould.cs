// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit locks for <see cref="DynamoDbSagaStore.PurgeCompletedBeforeAsync"/> and
/// <see cref="DynamoDbSagaStore.PurgeAllTenantsCompletedBeforeAsync"/> (bead vd50j3, vtklu8).
/// </summary>
/// <remarks>
/// <para>
/// Reference: mirrors the cross-provider purge contract asserted behaviorally by the shared
/// <c>SagaStoreConformanceTestBase</c> purge test (d0wpug/w8aqq3). Because DynamoDB exposes an
/// <see cref="IAmazonDynamoDB"/> seam, this store is driven through a faked client — the purge is
/// exercised end-to-end (Scan → BatchWriteItem) rather than only source-asserted.
/// </para>
/// <para>
/// Contract: <c>PurgeCompletedBeforeAsync(threshold, ct)</c> deletes ONLY sagas that are completed
/// (<c>completedAt</c> present) AND aged (<c>completedAt &lt; threshold</c>) AND owned by the CALLING
/// tenant, NEVER a running saga (<c>completedAt</c> absent) and NEVER another tenant's completed sagas,
/// and returns the deleted count. It never refuses: <see cref="TenantScope.TenantId"/> is total, so
/// untenanted, the single-tenant default, and a real tenant all bind a concrete predicate value.
/// <c>PurgeAllTenantsCompletedBeforeAsync</c> is the one estate-wide sweep that applies no tenant
/// predicate at all, reachable only by calling it directly.
/// </para>
/// <para>
/// NON-VACUITY: the running-saga exclusion is the load-bearing guard, enforced server-side by the
/// <c>attribute_exists(#completedAt)</c> predicate in the Scan <c>FilterExpression</c>; the cross-tenant
/// exclusion by the <c>#t = :tenantId</c> predicate, bound to the CURRENT tenant scope, not the caller's
/// payload. A mutant that drops <c>attribute_exists</c> (letting running sagas match), the
/// <c>#c &lt; :cutoff</c> bound (purging not-yet-aged completed sagas), or the tenant predicate (a scoped
/// call reverts to deleting every tenant's rows) changes the captured request and turns the filter
/// assertions RED. The behavioral tests additionally fail if the impl no-ops (count 0 vs 1) or fails to
/// issue the delete for a matched item.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
public sealed class DynamoDbSagaPurgeShould
{
	private const string TableName = "sagas";

	private static DynamoDbSagaStore CreateStore(IAmazonDynamoDB client, ITenantContext? tenantContext = null)
	{
		var options = Options.Create(new DynamoDbSagaOptions { TableName = TableName });
		return new DynamoDbSagaStore(
			client,
			options,
			A.Fake<ILogger<DynamoDbSagaStore>>(),
			new DispatchJsonSerializer(),
			tenantContext: tenantContext ?? TestTenantContext.Untenanted);
	}

	/// <summary>A context pinned to a real, named tenant, for the scoped-purge tenant-predicate locks.</summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}

	private static Dictionary<string, AttributeValue> KeyItem(string pk, string sk) => new()
	{
		[DynamoDbSagaDocument.PK] = new AttributeValue { S = pk },
		[DynamoDbSagaDocument.SK] = new AttributeValue { S = sk },
	};

	[Fact]
	public async Task Purge_FiltersOnAttributeExistsAndAgeBound_ExcludingRunningSagas()
	{
		// Arrange — capture the Scan request the impl issues; return no matches (server-side filtered).
		ScanRequest? captured = null;
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.ReturnsLazily((ScanRequest r, CancellationToken _) =>
			{
				captured = r;
				return Task.FromResult(new ScanResponse { Items = [], LastEvaluatedKey = [] });
			});

		var store = CreateStore(client);
		var threshold = new DateTimeOffset(2026, 07, 04, 12, 00, 00, TimeSpan.Zero);

		// Act
		var removed = await store.PurgeCompletedBeforeAsync(threshold, CancellationToken.None);

		// Assert — the load-bearing running-saga guard is the attribute_exists predicate + age bound, plus
		// the tenant predicate for the current (untenanted) scope.
		removed.ShouldBe(0);
		captured.ShouldNotBeNull();
		captured!.FilterExpression.ShouldBe("attribute_exists(#c) AND #c < :cutoff AND #t = :tenantId");
		captured.ExpressionAttributeNames["#c"].ShouldBe(DynamoDbSagaDocument.CompletedAt);
		captured.ExpressionAttributeNames["#t"].ShouldBe(DynamoDbSagaDocument.TenantId);
		captured.ExpressionAttributeValues[":cutoff"].S
			.ShouldBe(threshold.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
		captured.ExpressionAttributeValues[":tenantId"].S.ShouldBe(TenantScope.UntenantedSentinel);
	}

	[Theory]
	[InlineData("tenant-a")]
	public async Task Purge_AppliesTheCurrentTenantAsAServerSidePredicate_ForARealTenant(string tenantId)
	{
		// RED against the pre-fix code: a store scoped to a real, named tenant used to REFUSE this call
		// with TenantScopeNotSupportedException. It must now filter instead.
		ScanRequest? captured = null;
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.ReturnsLazily((ScanRequest r, CancellationToken _) =>
			{
				captured = r;
				return Task.FromResult(new ScanResponse { Items = [], LastEvaluatedKey = [] });
			});

		var store = CreateStore(client, new FixedTenantContext(tenantId));

		var removed = await store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		removed.ShouldBe(0);
		captured.ShouldNotBeNull();
		captured!.FilterExpression.ShouldBe("attribute_exists(#c) AND #c < :cutoff AND #t = :tenantId");
		captured.ExpressionAttributeValues[":tenantId"].S.ShouldBe(tenantId);
	}

	[Fact]
	public async Task PurgeAllTenantsCompletedBeforeAsync_AppliesNoTenantPredicate()
	{
		// The estate-wide sweep, called directly, applies no tenant term even when the ambient scope names
		// a real tenant -- distinguishing it from the scoped purge above.
		ScanRequest? captured = null;
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.ReturnsLazily((ScanRequest r, CancellationToken _) =>
			{
				captured = r;
				return Task.FromResult(new ScanResponse { Items = [], LastEvaluatedKey = [] });
			});

		var store = CreateStore(client, new FixedTenantContext("tenant-a"));

		_ = await store.PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured!.FilterExpression.ShouldBe("attribute_exists(#c) AND #c < :cutoff");
	}

	[Fact]
	public async Task Purge_DeletesMatchedCompletedAgedSaga_AndReturnsCount()
	{
		// Arrange — the server (honoring attribute_exists + age bound) returns exactly one eligible key.
		var eligible = KeyItem("SAGA#old", "TYPE#TestSagaState");
		BatchWriteItemRequest? deleteRequest = null;

		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ScanResponse { Items = [eligible], LastEvaluatedKey = [] }));
		_ = A.CallTo(() => client.BatchWriteItemAsync(A<BatchWriteItemRequest>._, A<CancellationToken>._))
			.ReturnsLazily((BatchWriteItemRequest r, CancellationToken _) =>
			{
				deleteRequest = r;
				return Task.FromResult(new BatchWriteItemResponse { UnprocessedItems = [] });
			});

		var store = CreateStore(client);

		// Act
		var removed = await store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert — the matched saga is physically deleted and counted.
		removed.ShouldBe(1);
		deleteRequest.ShouldNotBeNull();
		var writes = deleteRequest!.RequestItems[TableName];
		writes.Count.ShouldBe(1);
		var deletedKey = writes[0].DeleteRequest.Key;
		deletedKey[DynamoDbSagaDocument.PK].S.ShouldBe("SAGA#old");
		deletedKey[DynamoDbSagaDocument.SK].S.ShouldBe("TYPE#TestSagaState");
	}

	[Fact]
	public async Task Purge_EmptyResult_RemovesNothing()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ScanResponse { Items = [], LastEvaluatedKey = [] }));

		var store = CreateStore(client);

		var removed = await store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		removed.ShouldBe(0);
		A.CallTo(() => client.BatchWriteItemAsync(A<BatchWriteItemRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}
