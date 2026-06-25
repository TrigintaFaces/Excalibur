// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb.Projections;

/// <summary>
/// Author≠impl regression lock for S848 Lane P2 (bead <c>cvpmn1</c>): correctness of
/// <see cref="DynamoDbProjectionStore{TProjection}.QueryCursorAsync"/> cursor pagination.
/// </summary>
/// <remarks>
/// <para>
/// Pairs with the Backend fix in
/// <c>src/Excalibur/Excalibur.Data.DynamoDb/Projections/DynamoDbProjectionStore.cs</c>
/// (<c>QueryCursorAsync</c>, ~L280–364).
/// </para>
/// <para>
/// <b>Pre-fix behavior these tests RED-prove</b> (cited from the current impl):
/// <list type="bullet">
/// <item>A <c>Select.COUNT</c> full-table scan is issued <em>per page</em> (L320–337) — O(n²)
/// scan work over an N-page walk (violates AC-P2.3 / FR-P2.1).</item>
/// <item>The item scan sets <c>Limit = pageSize</c> (L303), and DynamoDB Scan <c>Limit</c> caps items
/// <em>scanned</em> not <em>matched</em> (pre-filter). With sparse matches a page returns fewer than
/// <c>pageSize</c> matched items while still emitting a non-null cursor (violates AC-P2.1 / FR-P2.3).</item>
/// <item>The total is read from a single <c>countResponse.Count</c> (L342–343); when a COUNT scan
/// truncates at the 1 MB boundary (partial <c>Count</c> + non-null <c>LastEvaluatedKey</c>), that partial
/// is presented as the total (violates AC-P2.2 / FR-P2.2).</item>
/// </list>
/// </para>
/// <para>
/// <b>Seam:</b> a hand-scripted <see cref="IAmazonDynamoDB"/> fake (<see cref="ScriptedDynamoClient"/>)
/// that (a) distinguishes <c>Select.COUNT</c> scans from item scans, (b) serves scripted continuation
/// pages keyed off <see cref="ScanRequest.ExclusiveStartKey"/>, and (c) <em>counts</em> how many scans
/// of each kind were issued. This is dependency-independent — fixtures are hand-built attribute maps
/// driven through the store's own <c>QueryCursorAsync</c> logic; no container required.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Data)]
[Trait("Database", TestInfrastructure.DynamoDb)]
public sealed class DynamoDbProjectionStoreCursorShould
{
	private const string PartitionKeyName = "PK";

	/// <summary>A minimal consumer projection. Type name drives the scan filter discriminator.</summary>
	private sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;

		public bool Match { get; set; }
	}

	private static IOptions<DynamoDbProjectionStoreOptions> CreateOptions() =>
		Microsoft.Extensions.Options.Options.Create(new DynamoDbProjectionStoreOptions
		{
			TableName = "Projections",
			PartitionKeyName = PartitionKeyName,
			// Disable auto-create so DescribeTable/CreateTable calls never pollute scan-count assertions.
			AutoCreateTable = false,
		});

	private static DynamoDbProjectionStore<TestProjection> CreateStore(IAmazonDynamoDB client) =>
		new(client, CreateOptions(), NullLogger<DynamoDbProjectionStore<TestProjection>>.Instance);

	/// <summary>
	/// Builds a DynamoDB item attribute map shaped exactly like <c>UpsertAsync</c> would write it:
	/// root-level projection properties, the compound PK, and the nested <c>_projection</c> metadata
	/// map carrying the type discriminator the scan filter matches on.
	/// </summary>
	private static Dictionary<string, AttributeValue> BuildItem(string id, bool match)
	{
		var projectionType = typeof(TestProjection).Name;
		return new Dictionary<string, AttributeValue>
		{
			[PartitionKeyName] = new() { S = $"{projectionType}#{id}" },
			["id"] = new() { S = id },
			["match"] = new() { BOOL = match },
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

	private static Dictionary<string, AttributeValue> KeyFor(string id)
	{
		var projectionType = typeof(TestProjection).Name;
		return new Dictionary<string, AttributeValue>
		{
			[PartitionKeyName] = new() { S = $"{projectionType}#{id}" },
		};
	}

	/// <summary>
	/// Hand-scripted DynamoDB client. Item scans walk <paramref name="_itemPages"/> by
	/// <see cref="ScanRequest.ExclusiveStartKey"/>; COUNT scans (<see cref="Select.COUNT"/>) return
	/// scripted <see cref="ScanResponse.Count"/> values. Both kinds are tallied.
	/// </summary>
	private sealed class ScriptedDynamoClient : AmazonDynamoDBClient
	{
		private readonly IReadOnlyList<ScanResponse> _itemPages;
		private readonly IReadOnlyList<ScanResponse> _countPages;
		private int _itemCallIndex;
		private int _countCallIndex;

		public ScriptedDynamoClient(IReadOnlyList<ScanResponse> itemPages, IReadOnlyList<ScanResponse> countPages)
			// Dummy static creds + region so the base ctor does not probe the environment.
			: base("AKIDTEST", "secret", Amazon.RegionEndpoint.USEast1) // pragma: allowlist secret
		{
			_itemPages = itemPages;
			_countPages = countPages;
		}

		public int ItemScanCount { get; private set; }

		public int CountScanCount { get; private set; }

		public override Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
		{
			if (request.Select == Select.COUNT)
			{
				CountScanCount++;
				var idx = Math.Min(_countCallIndex, _countPages.Count - 1);
				_countCallIndex++;
				return Task.FromResult(_countPages[idx]);
			}

			ItemScanCount++;
			var pageIdx = Math.Min(_itemCallIndex, _itemPages.Count - 1);
			_itemCallIndex++;
			return Task.FromResult(_itemPages[pageIdx]);
		}
	}

	// ---------------------------------------------------------------------------------------------
	// AC-P2.1 / FR-P2.3 — page fill: a sparse-match page MUST return up to pageSize MATCHED items,
	// not a short/empty page with a non-null cursor mid-stream.
	// ---------------------------------------------------------------------------------------------
	[Fact]
	public async Task FillPageToPageSizeWhenMatchesAreSparse()
	{
		// Arrange: 10-row table, only the LAST row matches the requested type. The current impl
		// scans Limit=pageSize rows (which DynamoDB caps as items SCANNED, not MATCHED), so a single
		// scan returns at most a few matched items and a non-null LastEvaluatedKey mid-stream.
		const int pageSize = 5;

		// Page 1 of the scan: only 1 matched item is visible (sparse), continuation key set.
		var itemPage1 = new ScanResponse
		{
			Items = [BuildItem("m1", match: true)],
			LastEvaluatedKey = KeyFor("scanned5"),
		};
		// Page 2 (only reached by a fixed impl that follows LastEvaluatedKey until pageSize matched):
		var itemPage2 = new ScanResponse
		{
			Items =
			[
				BuildItem("m2", match: true),
				BuildItem("m3", match: true),
				BuildItem("m4", match: true),
				BuildItem("m5", match: true),
			],
			LastEvaluatedKey = [],
		};
		var countResp = new ScanResponse { Count = 5 };

		var client = new ScriptedDynamoClient([itemPage1, itemPage2], [countResp]);
		var store = CreateStore(client);

		// Act
		var result = await store.QueryCursorAsync(filters: null, cursor: null, pageSize, CancellationToken.None);

		// Assert: a correct store fills the page to pageSize matched items (5), not a short page (1).
		result.Items.Count().ShouldBe(pageSize);
	}

	// ---------------------------------------------------------------------------------------------
	// AC-P2.3 / FR-P2.1 — over N pages, NO per-page full-table COUNT scan (scan work not O(n²)).
	// ---------------------------------------------------------------------------------------------
	[Fact]
	public async Task NotIssueAFullCountScanPerPageOverMultiplePages()
	{
		// Arrange: a 3-page walk. Each page has pageSize matched items + a continuation cursor
		// (last page null). A correct impl computes the total at most ONCE for the walk (or omits it),
		// so COUNT scans MUST NOT scale with the number of pages walked.
		const int pageSize = 2;

		ScanResponse ItemPage(string a, string b, bool more) => new()
		{
			Items = [BuildItem(a, true), BuildItem(b, true)],
			LastEvaluatedKey = more ? KeyFor(b) : [],
		};

		var client = new ScriptedDynamoClient(
			itemPages: [ItemPage("a", "b", more: true), ItemPage("c", "d", more: true), ItemPage("e", "f", more: false)],
			countPages: [new ScanResponse { Count = 6 }]);
		var store = CreateStore(client);

		// Act: walk all 3 pages following the cursor.
		string? cursor = null;
		var pagesWalked = 0;
		do
		{
			var page = await store.QueryCursorAsync(filters: null, cursor, pageSize, CancellationToken.None);
			cursor = page.NextCursor;
			pagesWalked++;
		}
		while (cursor is not null && pagesWalked < 10);

		// Assert: 3 pages walked, but at most ONE COUNT scan across the whole walk (not one per page).
		pagesWalked.ShouldBe(3);
		client.CountScanCount.ShouldBeLessThanOrEqualTo(1);
	}

	// ---------------------------------------------------------------------------------------------
	// AC-P2.2 / FR-P2.2 — total MUST NOT be a silently-truncated partial when the COUNT scan
	// crosses the 1 MB boundary (partial Count + non-null LastEvaluatedKey).
	// ---------------------------------------------------------------------------------------------
	[Fact]
	public async Task NotReportATruncatedPartialAsTheTotalCount()
	{
		// Arrange: the COUNT scan truncates at the 1 MB boundary — it returns a PARTIAL count of 100
		// with a non-null LastEvaluatedKey signalling more rows exist. The true total is 250
		// (the continuation count scan). The current impl reads only the first partial Count.
		const int pageSize = 10;
		const int truncatedPartial = 100;
		const int trueTotal = 250;

		var itemPage = new ScanResponse
		{
			Items = [BuildItem("x1", true)],
			LastEvaluatedKey = [],
		};

		// First COUNT response is truncated (partial + continuation); a correct impl follows the
		// continuation to reach the true total, or omits the total — but never presents 100 as the total.
		var countPartial = new ScanResponse { Count = truncatedPartial, LastEvaluatedKey = KeyFor("countCursor") };
		var countFinal = new ScanResponse { Count = trueTotal - truncatedPartial, LastEvaluatedKey = [] };

		var client = new ScriptedDynamoClient([itemPage], [countPartial, countFinal]);
		var store = CreateStore(client);

		// Act
		var result = await store.QueryCursorAsync(filters: null, cursor: null, pageSize, CancellationToken.None);

		// Assert: the reported total must NOT be the silently-truncated partial (100).
		// A correct impl either reports the true total (250) or omits it — never the partial.
		result.TotalRecords.ShouldNotBe(truncatedPartial);
	}

	// ---------------------------------------------------------------------------------------------
	// AC-P2.4 / FR-P2.3 — last page: LastEvaluatedKey null ⇒ cursor reported exhausted
	// (no phantom next-page cursor).
	// ---------------------------------------------------------------------------------------------
	[Fact]
	public async Task ReportCursorExhaustedOnLastPage()
	{
		// Arrange: a single full page with NO continuation key — this is the last page.
		const int pageSize = 3;
		var lastPage = new ScanResponse
		{
			Items = [BuildItem("a", true), BuildItem("b", true), BuildItem("c", true)],
			LastEvaluatedKey = [],
		};
		var client = new ScriptedDynamoClient([lastPage], [new ScanResponse { Count = 3 }]);
		var store = CreateStore(client);

		// Act
		var result = await store.QueryCursorAsync(filters: null, cursor: null, pageSize, CancellationToken.None);

		// Assert
		result.NextCursor.ShouldBeNull();
		result.HasMore.ShouldBeFalse();
	}

	// ---------------------------------------------------------------------------------------------
	// EC-P2.1 — zero matches across the whole table ⇒ single exhausted page, null cursor, total 0.
	// ---------------------------------------------------------------------------------------------
	[Fact]
	public async Task ReturnSingleExhaustedPageWithZeroTotalWhenNoMatches()
	{
		// Arrange: the scan walks the whole table but finds NO matched items. A correct impl returns
		// an empty page with a null cursor and total 0 — not a short page with a phantom cursor.
		const int pageSize = 5;

		// First scan: empty matches but a continuation key (rows scanned, none matched).
		var scanPage1 = new ScanResponse { Items = [], LastEvaluatedKey = KeyFor("scanned5") };
		// Second scan: table exhausted, still no matches.
		var scanPage2 = new ScanResponse { Items = [], LastEvaluatedKey = [] };

		var client = new ScriptedDynamoClient([scanPage1, scanPage2], [new ScanResponse { Count = 0 }]);
		var store = CreateStore(client);

		// Act
		var result = await store.QueryCursorAsync(filters: null, cursor: null, pageSize, CancellationToken.None);

		// Assert
		result.Items.ShouldBeEmpty();
		result.NextCursor.ShouldBeNull();
		result.TotalRecords.ShouldBe(0);
	}

	// ---------------------------------------------------------------------------------------------
	// EC-P2.2 — all rows match ⇒ standard pagination, correct total reported.
	// ---------------------------------------------------------------------------------------------
	[Fact]
	public async Task ReportCorrectTotalWhenAllRowsMatch()
	{
		// Arrange: a full first page of all-matching rows with more available; total is 4.
		const int pageSize = 2;
		var itemPage = new ScanResponse
		{
			Items = [BuildItem("a", true), BuildItem("b", true)],
			LastEvaluatedKey = KeyFor("b"),
		};
		var client = new ScriptedDynamoClient([itemPage], [new ScanResponse { Count = 4 }]);
		var store = CreateStore(client);

		// Act
		var result = await store.QueryCursorAsync(filters: null, cursor: null, pageSize, CancellationToken.None);

		// Assert: a full page of matched items and the correct (non-truncated) total.
		result.Items.Count().ShouldBe(pageSize);
		result.TotalRecords.ShouldBe(4);
	}
}
