// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Threading;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb.Projections;

/// <summary>
/// Regression lock for scan pagination in
/// <see cref="DynamoDbProjectionStore{TProjection}.QueryAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> the returned list is every projection matching the filter — or, when a
/// <c>Take</c> is supplied, exactly that many matched projections while any remain.
/// </para>
/// <para>
/// <b>The defect this locks:</b> a single <c>Scan</c> was issued and its items returned.
/// <c>Scan</c> reads a bounded amount of the table and reports the remainder with
/// <c>LastEvaluatedKey</c>, which was never consulted — so the caller received the projections that
/// happened to fall in the first response, with nothing to distinguish that from the whole set.
/// <c>Take</c> made it worse rather than better: it was passed through as the scan <c>Limit</c>, which
/// caps items <b>scanned</b> and is applied <b>before</b> the filter, so a sparse match returned far
/// fewer than the caller asked for while more were still there.
/// </para>
/// <para>
/// <b>Found by sweep, not by report:</b> the same "one request is the population" mistake as the CDC
/// shard-discovery and checkpoint-scan defects, in the same package. The sibling methods on this very
/// type (<c>QueryPagedAsync</c>, <c>QueryCursorAsync</c>, <c>ComputeTotalAsync</c>) already follow the
/// continuation; this one did not.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> both <see cref="ReturnMatchesFromEveryScanPage"/> and
/// <see cref="FillTakeAcrossPages_WhenMatchesAreSparse"/> are RED against a single-request
/// implementation. <see cref="NotScanAgain_WhenThereIsNoContinuation"/> is the liveness counterweight —
/// it fails against an implementation that scans forever.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Data)]
[Trait("Database", TestInfrastructure.DynamoDb)]
public sealed class DynamoDbProjectionStoreQueryPaginationShould
{
	private const string PartitionKeyName = "PK";

	private sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>
	/// THE ARM THAT MATTERS. Matches on a later scan page are returned.
	/// </summary>
	[Fact]
	public async Task ReturnMatchesFromEveryScanPage()
	{
		var client = new PagedScanClient(
		[
			new ScanResponse { Items = [BuildItem("p1")], LastEvaluatedKey = KeyFor("p1") },
			new ScanResponse { Items = [BuildItem("p2")], LastEvaluatedKey = KeyFor("p2") },
			new ScanResponse { Items = [BuildItem("p3")], LastEvaluatedKey = [] },
		]);

		var results = await CreateStore(client).QueryAsync(filters: null, options: null, CancellationToken.None);

		results.Select(r => r.Id).ShouldBe(
			["p1", "p2", "p3"],
			ignoreOrder: true,
			customMessage: "a projection past the first scan page is silently dropped, and the returned "
				+ "list gives the caller no way to tell a complete answer from a truncated one.");

		client.StartKeys.ShouldBe(
			[null, "p1", "p2"],
			"each request must resume at the key the previous response stopped on.");
	}

	/// <summary>
	/// SAFETY. <c>Take</c> caps MATCHED projections and is filled across pages.
	/// </summary>
	/// <remarks>
	/// Scan's <c>Limit</c> caps items scanned, pre-filter, so passing <c>Take</c> straight through
	/// returns a short list whenever matches are sparse — the caller asked for three and silently got one.
	/// </remarks>
	[Fact]
	public async Task FillTakeAcrossPages_WhenMatchesAreSparse()
	{
		var client = new PagedScanClient(
		[
			new ScanResponse { Items = [BuildItem("m1")], LastEvaluatedKey = KeyFor("m1") },
			new ScanResponse { Items = [], LastEvaluatedKey = KeyFor("scanned-nothing-matched") },
			new ScanResponse { Items = [BuildItem("m2"), BuildItem("m3")], LastEvaluatedKey = [] },
		]);

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			options: new QueryOptions(Take: 3),
			CancellationToken.None);

		results.Count.ShouldBe(
			3,
			"Take is a cap on MATCHED projections; a page that scanned rows and matched none is not the "
			+ "end of the table.");
	}

	/// <summary>
	/// PRECISION. <c>Take</c> stops the walk as soon as it is satisfied — it does not drain the table.
	/// </summary>
	[Fact]
	public async Task StopScanning_OnceTakeIsSatisfied()
	{
		var client = new PagedScanClient(
		[
			new ScanResponse { Items = [BuildItem("m1"), BuildItem("m2")], LastEvaluatedKey = KeyFor("m2") },
			new ScanResponse { Items = [BuildItem("m3")], LastEvaluatedKey = [] },
		]);

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			options: new QueryOptions(Take: 2),
			CancellationToken.None);

		results.Count.ShouldBe(2);
		client.ScanCount.ShouldBe(1, "a satisfied Take must not keep reading the table.");
	}

	/// <summary>
	/// LIVENESS counterweight. A response with no continuation ends the walk.
	/// </summary>
	[Fact]
	public async Task NotScanAgain_WhenThereIsNoContinuation()
	{
		var client = new PagedScanClient([new ScanResponse { Items = [BuildItem("only")], LastEvaluatedKey = [] }]);

		var results = await CreateStore(client).QueryAsync(filters: null, options: null, CancellationToken.None);

		results.Count.ShouldBe(1);
		client.ScanCount.ShouldBe(1, "one page, one scan.");
	}

	// ─── Fixture ────────────────────────────────────────────────────────────

	private static DynamoDbProjectionStore<TestProjection> CreateStore(IAmazonDynamoDB client) =>
		new(
			client,
			Microsoft.Extensions.Options.Options.Create(new DynamoDbProjectionStoreOptions
			{
				TableName = "Projections",
				PartitionKeyName = PartitionKeyName,
				AutoCreateTable = false,
			}),
			NullLogger<DynamoDbProjectionStore<TestProjection>>.Instance);

	private static Dictionary<string, AttributeValue> BuildItem(string id)
	{
		var projectionType = typeof(TestProjection).Name;
		return new Dictionary<string, AttributeValue>
		{
			[PartitionKeyName] = new() { S = $"{projectionType}#{id}" },
			["id"] = new() { S = id },
			["_projection"] = new()
			{
				M = new Dictionary<string, AttributeValue>
				{
					["id"] = new() { S = id },
					["type"] = new() { S = projectionType },
					["updatedAt"] = new() { S = DateTimeOffset.UtcNow.ToString("O") },
				},
			},
		};
	}

	private static Dictionary<string, AttributeValue> KeyFor(string id) =>
		new() { [PartitionKeyName] = new() { S = $"{typeof(TestProjection).Name}#{id}" } };

	/// <summary>
	/// Serves the scripted scan pages in order and records the <c>ExclusiveStartKey</c> each request
	/// carried, so the arms can assert the continuation was actually propagated.
	/// </summary>
	private sealed class PagedScanClient : AmazonDynamoDBClient
	{
		private readonly IReadOnlyList<ScanResponse> _pages;

		public PagedScanClient(IReadOnlyList<ScanResponse> pages)
			// Dummy static creds + region so the base ctor does not probe the environment.
			: base("AKIDTEST", "secret", Amazon.RegionEndpoint.USEast1) // pragma: allowlist secret
			=> _pages = pages;

		public int ScanCount { get; private set; }

		/// <summary>The partition-key value each request resumed from; <see langword="null"/> for the first.</summary>
		public List<string?> StartKeys { get; } = [];

		public override Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
		{
			StartKeys.Add(
				request.ExclusiveStartKey is { Count: > 0 } key && key.TryGetValue(PartitionKeyName, out var pk)
					? pk.S?.Split('#')[^1]
					: null);

			var index = Math.Min(ScanCount, _pages.Count - 1);
			ScanCount++;

			return Task.FromResult(_pages[index]);
		}
	}
}
