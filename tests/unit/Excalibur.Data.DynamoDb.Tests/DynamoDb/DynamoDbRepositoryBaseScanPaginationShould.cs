// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Threading;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Regression lock for the scan continuation on
/// <see cref="DynamoDbRepositoryBase{TDocument}.ScanAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> one scan request is issued, and the caller can always tell whether more
/// results remain. A non-<see langword="null"/> continuation token means DynamoDB stopped early;
/// <see langword="null"/> means the scan reached the end of the table.
/// </para>
/// <para>
/// <b>The defect this locks:</b> the method issued one <c>Scan</c> and returned its items as a bare
/// list, discarding <c>LastEvaluatedKey</c>. A <c>Scan</c> reads a bounded amount of the table, so a
/// correct-looking caller processed whatever fell in the first response with nothing to distinguish
/// that from the whole matching set. Silent truncation is worse than either alternative precisely
/// because nothing surfaces.
/// </para>
/// <para>
/// <b>Why report rather than drain:</b> the method takes a caller-supplied <see cref="ScanRequest"/>,
/// so the caller already owns the paging inputs (<c>Limit</c>, <c>ExclusiveStartKey</c>). Accepting
/// those and swallowing the paging output is an incoherent contract, and draining to exhaustion would
/// make a public member an unbounded read over a table of unknown size.
/// </para>
/// <para>
/// <b>Both arms are load-bearing.</b> The safety arm alone would pass an implementation that always
/// reported a continuation; the liveness arm alone would pass the original discard. Neither is
/// redundant.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Data)]
[Trait("Database", TestInfrastructure.DynamoDb)]
public sealed class DynamoDbRepositoryBaseScanPaginationShould
{
	private const string TestTableName = "scan-pagination-test";
	private const string TestPartitionKey = "pk";

	/// <summary>
	/// SAFETY. A scan DynamoDB stopped early reports the continuation, so the caller can discover
	/// that page 2 exists. RED against the original shape, which returned a bare list and had nowhere
	/// to put this.
	/// </summary>
	[Fact]
	public async Task ReportTheContinuationWhenMoreResultsRemain()
	{
		var lastEvaluatedKey = KeyFor("doc-1");
		var client = new ScriptedScanClient(new ScanResponse
		{
			Items = [ItemFor("doc-1", "first")],
			LastEvaluatedKey = lastEvaluatedKey,
			ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.5 },
		});

		var repository = new TestRepository(client);

		var result = await repository.ScanAsync(new ScanRequest(), CancellationToken.None);

		result.Documents.Count.ShouldBe(1);
		result.Documents[0].Name.ShouldBe("first");

		// The caller must be able to learn that more remain — this is the whole defect.
		result.ContinuationToken.ShouldNotBeNull();

		// And the token must actually carry the resume position, not merely be non-null: a token that
		// cannot be turned back into an ExclusiveStartKey reports "more exist" while stranding the caller.
		var decoded = JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(result.ContinuationToken!);
		decoded.ShouldNotBeNull();
		decoded!.ShouldContainKey(TestPartitionKey);
		decoded[TestPartitionKey].S.ShouldBe(lastEvaluatedKey[TestPartitionKey].S);

		result.RequestCharge.ShouldBe(2.5);

		// Exactly one request: the contract reports the continuation, it does not drain.
		client.ScanCount.ShouldBe(1);
	}

	/// <summary>
	/// LIVENESS. A scan that reached the end of the table still returns its documents and reports
	/// exhaustion with a null continuation. Without this arm, "always hand back a continuation" would
	/// satisfy the safety arm above while telling every caller there is another page forever.
	/// </summary>
	[Fact]
	public async Task ReturnItemsAndReportExhaustionWhenNoMoreResultsRemain()
	{
		var client = new ScriptedScanClient(new ScanResponse
		{
			Items = [ItemFor("doc-1", "only"), ItemFor("doc-2", "also")],
			LastEvaluatedKey = [],
			ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 },
		});

		var repository = new TestRepository(client);

		var result = await repository.ScanAsync(new ScanRequest(), CancellationToken.None);

		result.Documents.Count.ShouldBe(2);
		result.Documents.Select(static d => d.Name).ShouldBe(["only", "also"]);
		result.ContinuationToken.ShouldBeNull();
		client.ScanCount.ShouldBe(1);
	}

	/// <summary>
	/// The continuation the caller gets back is accepted as the resume position on the next request,
	/// so paging actually advances rather than re-reading page 1.
	/// </summary>
	[Fact]
	public async Task ResumeFromTheContinuationOnTheFollowingRequest()
	{
		var firstPage = new ScanResponse
		{
			Items = [ItemFor("doc-1", "first")],
			LastEvaluatedKey = KeyFor("doc-1"),
		};
		var secondPage = new ScanResponse
		{
			Items = [ItemFor("doc-2", "second")],
			LastEvaluatedKey = [],
		};

		var client = new ScriptedScanClient(firstPage, secondPage);
		var repository = new TestRepository(client);

		var page1 = await repository.ScanAsync(new ScanRequest(), CancellationToken.None);
		page1.ContinuationToken.ShouldNotBeNull();

		var resumed = new ScanRequest
		{
			ExclusiveStartKey = JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(page1.ContinuationToken!)!,
		};
		var page2 = await repository.ScanAsync(resumed, CancellationToken.None);

		page2.Documents.Count.ShouldBe(1);
		page2.Documents[0].Name.ShouldBe("second");
		page2.ContinuationToken.ShouldBeNull();

		// The second request carried the first page's stopping point.
		client.StartKeys.Count.ShouldBe(2);
		client.StartKeys[0].ShouldBeNull();
		client.StartKeys[1].ShouldBe("doc-1");
	}

	private static Dictionary<string, AttributeValue> KeyFor(string id) =>
		new() { [TestPartitionKey] = new AttributeValue { S = id } };

	private static Dictionary<string, AttributeValue> ItemFor(string id, string name) =>
		new()
		{
			[TestPartitionKey] = new AttributeValue { S = id },
			["name"] = new AttributeValue { S = name },
		};

	private sealed class TestDocument
	{
		public string Name { get; set; } = string.Empty;
	}

	private sealed class TestRepository(IAmazonDynamoDB client)
		: DynamoDbRepositoryBase<TestDocument>(client, TestTableName, TestPartitionKey)
	{
		/// <summary>Not exercised here: these arms script the scan responses directly.</summary>
		public override Task InitializeTableAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}

	/// <summary>
	/// Serves the scripted scan responses in order and records the <c>ExclusiveStartKey</c> each
	/// request carried, so the arms can assert the continuation was actually propagated.
	/// </summary>
	private sealed class ScriptedScanClient : AmazonDynamoDBClient
	{
		private readonly IReadOnlyList<ScanResponse> _pages;

		public ScriptedScanClient(params ScanResponse[] pages)
			// Dummy static creds + region so the base ctor does not probe the environment.
			: base("AKIDTEST", "secret", Amazon.RegionEndpoint.USEast1) // pragma: allowlist secret
			=> _pages = pages;

		public int ScanCount { get; private set; }

		/// <summary>The partition-key value each request resumed from; <see langword="null"/> for the first.</summary>
		public List<string?> StartKeys { get; } = [];

		public override Task<ScanResponse> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
		{
			StartKeys.Add(
				request.ExclusiveStartKey is { Count: > 0 } key && key.TryGetValue(TestPartitionKey, out var pk)
					? pk.S
					: null);

			var index = Math.Min(ScanCount, _pages.Count - 1);
			ScanCount++;

			return Task.FromResult(_pages[index]);
		}
	}
}
