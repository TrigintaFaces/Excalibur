// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb.Projections;

/// <summary>
/// Author≠impl regression lock for bd-eyg3je (Sprint 848 Lane P1, MS-1).
/// </summary>
/// <remarks>
/// <para>
/// Pre-fix behavior: <see cref="DynamoDbProjectionStore{TProjection}"/> <c>QueryAsync</c> and
/// <c>CountAsync</c> SILENTLY IGNORE the <c>filters</c> argument — the issued
/// <see cref="ScanRequest.FilterExpression"/> only ever carries the type discriminator
/// (<c>#proj.#type = :projType</c>), never the caller's predicate. That is a silent
/// correctness failure (returns/counts ALL projections of type T regardless of filter).
/// </para>
/// <para>
/// These tests assert the POST-FIX contract (FR-P1.1..P1.5, AC-P1.1..P1.5, EC-P1.1..P1.4):
/// the predicate MUST be AND-combined into the <c>FilterExpression</c> with correctly mapped
/// <see cref="ScanRequest.ExpressionAttributeNames"/>/<see cref="ScanRequest.ExpressionAttributeValues"/>,
/// and an untranslatable filter MUST throw <see cref="NotSupportedException"/> rather than
/// silently return unfiltered data. They are RED on current source (filters ignored) and GREEN
/// after Backend's fix in <c>DynamoDbProjectionStore.cs</c> (the coupled impl for bd-eyg3je).
/// </para>
/// <para>
/// Seam: a FakeItEasy <see cref="IAmazonDynamoDB"/> captures the <see cref="ScanRequest"/> the
/// store issues and returns a controlled <see cref="ScanResponse"/>. This RED-proves the bug
/// without a container (the bug is observable purely in the request the store builds), and is
/// fully deterministic.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbProjectionStoreFilterShould
{
	private const string MetadataKey = "_projection";
	private const string MetaFieldType = "type";

	/// <summary>A minimal projection with a string and a numeric filterable property.</summary>
	private sealed class RegionProjection
	{
		public string Id { get; set; } = string.Empty;

		public string Region { get; set; } = string.Empty;

		public int Score { get; set; }
	}

	private static IOptions<DynamoDbProjectionStoreOptions> CreateOptions() =>
		Microsoft.Extensions.Options.Options.Create(new DynamoDbProjectionStoreOptions
		{
			TableName = "Projections",
			// Skip EnsureTableAsync's DescribeTable round-trip so the fake only sees the Scan.
			AutoCreateTable = false,
		});

	private static DynamoDbProjectionStore<RegionProjection> CreateStore(IAmazonDynamoDB client) =>
		new(client, CreateOptions(), NullLogger<DynamoDbProjectionStore<RegionProjection>>.Instance);

	/// <summary>
	/// Captures every <see cref="ScanRequest"/> issued and returns an (optionally filtered)
	/// <see cref="ScanResponse"/>. The fake honors the request's <see cref="ScanRequest.Select"/>
	/// (COUNT vs item return) so count-shape assertions are meaningful.
	/// </summary>
	private static IAmazonDynamoDB CreateCapturingClient(
		List<ScanRequest> captured,
		IReadOnlyList<RegionProjection> dataset)
	{
		var client = A.Fake<IAmazonDynamoDB>();

		_ = A.CallTo(() => client.ScanAsync(A<ScanRequest>._, A<CancellationToken>._))
			.ReturnsLazily((ScanRequest req, CancellationToken _) =>
			{
				captured.Add(req);

				// The fake CANNOT evaluate a DynamoDB FilterExpression. It returns the full
				// dataset; the lock asserts on the *request the store built*, which is where
				// the bug lives. (A faithful filter engine is the job of DynamoDB, not this
				// unit lock — see don't-re-test-the-engine.)
				var items = dataset.Select(ToItem).ToList();

				return Task.FromResult(req.Select == Select.COUNT
					? new ScanResponse { Count = items.Count, HttpStatusCode = System.Net.HttpStatusCode.OK }
					: new ScanResponse { Items = items, Count = items.Count, HttpStatusCode = System.Net.HttpStatusCode.OK });
			});

		return client;
	}

	/// <summary>
	/// Builds a DynamoDB item in the exact shape the store's read path expects: projection
	/// properties at the root, framework metadata nested under <c>_projection</c> (with the
	/// type discriminator), and the compound partition key at <c>PK</c>.
	/// </summary>
	private static Dictionary<string, AttributeValue> ToItem(RegionProjection p) => new()
	{
		["id"] = new AttributeValue { S = p.Id },
		["region"] = new AttributeValue { S = p.Region },
		["score"] = new AttributeValue { N = p.Score.ToString(CultureInfo.InvariantCulture) },
		["PK"] = new AttributeValue { S = $"{nameof(RegionProjection)}#{p.Id}" },
		[MetadataKey] = new AttributeValue
		{
			M = new Dictionary<string, AttributeValue>
			{
				["id"] = new AttributeValue { S = p.Id },
				[MetaFieldType] = new AttributeValue { S = nameof(RegionProjection) },
			},
		},
	};

	private static IReadOnlyList<RegionProjection> Dataset() =>
	[
		new() { Id = "1", Region = "us", Score = 10 },
		new() { Id = "2", Region = "eu", Score = 20 },
		new() { Id = "3", Region = "us", Score = 30 },
	];

	// ── AC-P1.1 / FR-P1.1 / FR-P1.3 ─────────────────────────────────────────────
	[Fact]
	public async Task QueryAsync_AppliesFilterPredicateInScanRequest()
	{
		// Arrange
		var captured = new List<ScanRequest>();
		var store = CreateStore(CreateCapturingClient(captured, Dataset()));
		var filters = new Dictionary<string, object> { ["region"] = "us" };

		// Act
		_ = await store.QueryAsync(filters, options: null, CancellationToken.None);

		// Assert — the store must AND-combine the predicate with the type discriminator.
		var request = captured.ShouldHaveSingleItem();
		request.FilterExpression.ShouldContain("#proj.#type = :projType");
		// Pre-fix: FilterExpression is ONLY the discriminator → these fail (RED).
		request.FilterExpression.ShouldContain("AND");
		request.ExpressionAttributeValues.Values
			.ShouldContain(v => v.S == "us", "the 'us' filter value must be bound into the scan");
	}

	// ── AC-P1.2 / FR-P1.2 ───────────────────────────────────────────────────────
	[Fact]
	public async Task CountAsync_AppliesFilterPredicateInScanRequest()
	{
		// Arrange
		var captured = new List<ScanRequest>();
		var store = CreateStore(CreateCapturingClient(captured, Dataset()));
		var filters = new Dictionary<string, object> { ["region"] = "us" };

		// Act
		_ = await store.CountAsync(filters, CancellationToken.None);

		// Assert
		var request = captured.ShouldHaveSingleItem();
		request.Select.ShouldBe(Select.COUNT);
		request.FilterExpression.ShouldContain("AND");
		request.ExpressionAttributeValues.Values.ShouldContain(v => v.S == "us");
	}

	// ── AC-P1.4 / FR-P1.1 ── null/empty filter → unchanged (discriminator only) ──
	[Fact]
	public async Task QueryAsync_WithNullFilter_IssuesDiscriminatorOnly()
	{
		var captured = new List<ScanRequest>();
		var store = CreateStore(CreateCapturingClient(captured, Dataset()));

		_ = await store.QueryAsync(filters: null, options: null, CancellationToken.None);

		var request = captured.ShouldHaveSingleItem();
		request.FilterExpression.ShouldBe("#proj.#type = :projType");
		request.FilterExpression.ShouldNotContain("AND");
	}

	[Fact]
	public async Task QueryAsync_WithEmptyFilter_IssuesDiscriminatorOnly()
	{
		var captured = new List<ScanRequest>();
		var store = CreateStore(CreateCapturingClient(captured, Dataset()));

		_ = await store.QueryAsync(new Dictionary<string, object>(), options: null, CancellationToken.None);

		var request = captured.ShouldHaveSingleItem();
		request.FilterExpression.ShouldBe("#proj.#type = :projType");
	}

	// ── EC-P1.1 ── multiple filter keys all AND-combined ─────────────────────────
	[Fact]
	public async Task QueryAsync_WithMultipleFilters_AndCombinesAllKeys()
	{
		var captured = new List<ScanRequest>();
		var store = CreateStore(CreateCapturingClient(captured, Dataset()));
		var filters = new Dictionary<string, object> { ["region"] = "us", ["score"] = 30 };

		_ = await store.QueryAsync(filters, options: null, CancellationToken.None);

		var request = captured.ShouldHaveSingleItem();
		// Discriminator + 2 predicates → at least two ANDs.
		var andCount = request.FilterExpression.Split("AND").Length - 1;
		andCount.ShouldBeGreaterThanOrEqualTo(2);
		request.ExpressionAttributeValues.Values.ShouldContain(v => v.S == "us");
		request.ExpressionAttributeValues.Values.ShouldContain(v => v.N == "30");
	}

	// ── EC-P1.2 ── non-string filter value maps to the correct attribute type ────
	[Fact]
	public async Task QueryAsync_WithNumericFilterValue_BindsNumberAttribute()
	{
		var captured = new List<ScanRequest>();
		var store = CreateStore(CreateCapturingClient(captured, Dataset()));
		var filters = new Dictionary<string, object> { ["score"] = 30 };

		_ = await store.QueryAsync(filters, options: null, CancellationToken.None);

		var request = captured.ShouldHaveSingleItem();
		// A numeric filter must bind a DynamoDB Number (N) attribute, not a String (S).
		request.ExpressionAttributeValues.Values.ShouldContain(v => v.N == "30");
	}

	// ── EC-P1.3 ── filter key == discriminator name → no expression-name collision ─
	[Fact]
	public async Task QueryAsync_WithFilterKeyMatchingDiscriminator_DoesNotCollide()
	{
		var captured = new List<ScanRequest>();
		var store = CreateStore(CreateCapturingClient(captured, Dataset()));
		// 'type' collides with the metadata discriminator field name used in the reserved
		// "#proj.#type = :projType" expression. The store must avoid reusing #type/:projType.
		var filters = new Dictionary<string, object> { ["type"] = "custom" };

		// Act — must not throw a collision/translation error, and must bind the predicate.
		_ = await store.QueryAsync(filters, options: null, CancellationToken.None);

		var request = captured.ShouldHaveSingleItem();
		request.FilterExpression.ShouldContain("AND");
		// The reserved discriminator binding must remain intact and unambiguous.
		request.ExpressionAttributeValues[":projType"].S.ShouldBe(nameof(RegionProjection));
		request.ExpressionAttributeValues.Values.ShouldContain(v => v.S == "custom");
	}

	// ── AC-P1.5 / FR-P1.5 ── untranslatable filter → NotSupportedException ────────
	[Fact]
	public async Task QueryAsync_WithUntranslatableFilter_ThrowsNotSupportedException()
	{
		var captured = new List<ScanRequest>();
		var store = CreateStore(CreateCapturingClient(captured, Dataset()));
		// A null filter VALUE cannot be translated to a DynamoDB equality predicate.
		// The store must signal "can't honor the contract" via NotSupportedException —
		// it must NOT silently fall back to an unfiltered scan.
		var filters = new Dictionary<string, object> { ["region"] = null! };

		_ = await Should.ThrowAsync<NotSupportedException>(async () =>
			await store.QueryAsync(filters, options: null, CancellationToken.None));

		// And it must NOT have silently issued an unfiltered scan.
		captured.ShouldBeEmpty();
	}
}
