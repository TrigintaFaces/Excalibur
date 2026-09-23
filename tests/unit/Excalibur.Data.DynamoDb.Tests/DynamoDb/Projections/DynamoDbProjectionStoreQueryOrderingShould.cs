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
/// Regression lock for <c>Skip</c>, <c>OrderBy</c> and <c>Descending</c> in
/// <see cref="DynamoDbProjectionStore{TProjection}.QueryAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> every member of <see cref="QueryOptions"/> that the caller supplies is either
/// honoured or refused. None of them may be accepted and then ignored.
/// </para>
/// <para>
/// <b>The defect this locks:</b> <c>QueryAsync</c> consumed <c>Take</c> and nothing else. A caller who
/// passed <c>Skip</c> or <c>OrderBy</c> received an unskipped, unordered page — with no exception, no
/// log and nothing in the returned list to distinguish it from a correct answer. That is the failure
/// mode worth locking: not a crash, but a wrong page that looks right.
/// </para>
/// <para>
/// <b>Why ordering forces a full drain.</b> DynamoDB's <c>Scan</c> has no server-side sort, so the
/// items arrive in partition order. Sorting a page that <c>Take</c> had already truncated would order
/// the wrong subset and return rows that are not the first by the requested key.
/// <see cref="OrderAcrossEveryScanPage_NotMerelyWithinOne"/> is the arm that binds this: it is RED
/// against any implementation that applies <c>Take</c> before sorting, including one that sorts
/// correctly within each page.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Every arm below is RED against the previous implementation, which read only
/// <c>options.Take</c>. <see cref="NotDrainEveryPage_WhenNoOrderingIsRequested"/> is the liveness
/// counterweight: it fails against a fix that bought correctness by always draining the table.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Data)]
[Trait("Database", TestInfrastructure.DynamoDb)]
public sealed class DynamoDbProjectionStoreQueryOrderingShould
{
	private const string PartitionKeyName = "PK";

	private sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;

		public int Rank { get; set; }
	}

	/// <summary>
	/// SAFETY. <c>OrderBy</c> orders the result instead of being discarded.
	/// </summary>
	[Fact]
	public async Task OrderByTheRequestedAttribute_WhenOrderByIsSupplied()
	{
		var client = SinglePage(Item("c", 3), Item("a", 1), Item("b", 2));

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			new QueryOptions(OrderBy: "rank"),
			CancellationToken.None);

		results.Select(static r => r.Id).ShouldBe(
			["a", "b", "c"],
			customMessage: "OrderBy was accepted and ignored: the caller received partition order, which "
				+ "is indistinguishable from a correctly ordered page.");
	}

	/// <summary>
	/// SAFETY. <c>Descending</c> reverses the order rather than being discarded.
	/// </summary>
	[Fact]
	public async Task ReverseTheOrder_WhenDescendingIsRequested()
	{
		var client = SinglePage(Item("a", 1), Item("c", 3), Item("b", 2));

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			new QueryOptions(OrderBy: "rank", Descending: true),
			CancellationToken.None);

		results.Select(static r => r.Id).ShouldBe(["c", "b", "a"]);
	}

	/// <summary>
	/// SAFETY. Numbers order numerically, not by their string form.
	/// </summary>
	/// <remarks>
	/// Stored numeric attributes are transported as strings, so an ordinal comparison puts 10 before 9.
	/// This arm is RED against a comparer that treats every attribute as text.
	/// </remarks>
	[Fact]
	public async Task OrderNumbersNumerically_NotLexicographically()
	{
		var client = SinglePage(Item("ten", 10), Item("nine", 9), Item("one", 1));

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			new QueryOptions(OrderBy: "rank"),
			CancellationToken.None);

		results.Select(static r => r.Id).ShouldBe(
			["one", "nine", "ten"],
			customMessage: "10 sorted before 9, so the attribute was compared as text rather than as a number.");
	}

	/// <summary>
	/// THE ARM THAT MATTERS. Ordering is applied to the whole matched set, not per scan page.
	/// </summary>
	/// <remarks>
	/// The highest-ranked projection is served on the LAST page. An implementation that applies
	/// <c>Take</c> before draining returns the first item it happens to see; only one that drains every
	/// page before sorting can return the true first row.
	/// </remarks>
	[Fact]
	public async Task OrderAcrossEveryScanPage_NotMerelyWithinOne()
	{
		var client = new PagedScanClient(
		[
			new ScanResponse { Items = [Item("mid", 5)], LastEvaluatedKey = KeyFor("mid") },
			new ScanResponse { Items = [Item("high", 9)], LastEvaluatedKey = KeyFor("high") },
			new ScanResponse { Items = [Item("low", 1)], LastEvaluatedKey = [] },
		]);

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			new QueryOptions(Take: 1, OrderBy: "rank"),
			CancellationToken.None);

		results.Select(static r => r.Id).ShouldBe(
			["low"],
			customMessage: "Take was applied before the sort, so the caller got the first item scanned "
				+ "rather than the first item by rank.");
	}

	/// <summary>
	/// SAFETY. <c>Skip</c> drops the requested prefix, and composes with <c>Take</c>.
	/// </summary>
	[Fact]
	public async Task SkipThenTake_WhenBothAreSupplied()
	{
		var client = SinglePage(Item("a", 1), Item("b", 2), Item("c", 3), Item("d", 4));

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			new QueryOptions(Skip: 1, Take: 2, OrderBy: "rank"),
			CancellationToken.None);

		results.Select(static r => r.Id).ShouldBe(["b", "c"]);
	}

	/// <summary>
	/// SAFETY. The caller may order by the CLR property name against the camelCased stored attribute.
	/// </summary>
	/// <remarks>
	/// Projections are written through a camelCase naming policy, so the property <c>Rank</c> is stored
	/// as <c>rank</c>. <see cref="QueryOptions.OrderBy"/> is documented as a property name, so matching
	/// only exactly would leave the result unordered — silently, which is the defect being fixed.
	/// </remarks>
	[Fact]
	public async Task ResolveTheClrPropertyName_AgainstTheCamelCasedAttribute()
	{
		var client = SinglePage(Item("c", 3), Item("a", 1), Item("b", 2));

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			new QueryOptions(OrderBy: "Rank"),
			CancellationToken.None);

		results.Select(static r => r.Id).ShouldBe(["a", "b", "c"]);
	}

	/// <summary>
	/// LIVENESS. Without ordering, <c>Take</c> still short-circuits instead of draining the table.
	/// </summary>
	/// <remarks>
	/// Refusing to stop early would satisfy every arm above while making an unordered page scan the
	/// whole table. This arm fails against that.
	/// </remarks>
	[Fact]
	public async Task NotDrainEveryPage_WhenNoOrderingIsRequested()
	{
		var client = new PagedScanClient(
		[
			new ScanResponse { Items = [Item("a", 1)], LastEvaluatedKey = KeyFor("a") },
			new ScanResponse { Items = [Item("b", 2)], LastEvaluatedKey = KeyFor("b") },
			new ScanResponse { Items = [Item("c", 3)], LastEvaluatedKey = [] },
		]);

		var results = await CreateStore(client).QueryAsync(
			filters: null,
			new QueryOptions(Take: 1),
			CancellationToken.None);

		results.Count.ShouldBe(1);
		client.ScanCount.ShouldBe(1, "an unordered Take must stop at the first page that fills it.");
	}

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

	private static PagedScanClient SinglePage(params Dictionary<string, AttributeValue>[] items) =>
		new([new ScanResponse { Items = [.. items], LastEvaluatedKey = [] }]);

	private static Dictionary<string, AttributeValue> Item(string id, int rank)
	{
		var projectionType = typeof(TestProjection).Name;
		return new Dictionary<string, AttributeValue>
		{
			[PartitionKeyName] = new() { S = $"{projectionType}#{id}" },
			["id"] = new() { S = id },
			["rank"] = new() { N = rank.ToString(System.Globalization.CultureInfo.InvariantCulture) },
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
	/// Serves scripted scan pages in order and counts the requests, so the arms can assert both what
	/// was returned and how much of the table was read to return it.
	/// </summary>
	private sealed class PagedScanClient : AmazonDynamoDBClient
	{
		private readonly IReadOnlyList<ScanResponse> _pages;

		public PagedScanClient(IReadOnlyList<ScanResponse> pages)
			// Dummy static creds + region so the base ctor does not probe the environment.
			: base("AKIDTEST", "secret", Amazon.RegionEndpoint.USEast1) // pragma: allowlist secret
			=> _pages = pages;

		public int ScanCount { get; private set; }

		public override Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
		{
			var index = Math.Min(ScanCount, _pages.Count - 1);
			ScanCount++;

			return Task.FromResult(_pages[index]);
		}
	}
}
