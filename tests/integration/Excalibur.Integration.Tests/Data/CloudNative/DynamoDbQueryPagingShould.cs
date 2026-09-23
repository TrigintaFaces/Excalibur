// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CloudNative;
using Excalibur.Data.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.CloudNative;

/// <summary>
/// Binds the rule that a cloud-native query can be <em>advanced</em>: the continuation token a page returns
/// is accepted back by the public API and yields the next page.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks:</b> the provider populated
/// <see cref="CloudQueryResult{TDocument}.ContinuationToken"/> and reported
/// <see cref="CloudQueryResult{TDocument}.HasMoreResults"/> off it, while the query method took no
/// parameter that accepted a token. A caller was told more results existed, handed a token, and given no
/// API to pass it to, so the second page was unreachable and the token was decorative.
/// </para>
/// <para>
/// <b>Why this arm is non-vacuous by construction:</b> it could not be authored against the previous API at
/// all — there was no overload to pass a token to, so the test would not have compiled. That is a stronger
/// guarantee than a mutation, because the failure was in the shape of the contract rather than in a value.
/// </para>
/// <para>
/// <b>Why it must run against the real service.</b> The token is not ours: it is the serialized
/// <c>LastEvaluatedKey</c> DynamoDB itself returns, and the test asserts the service accepts back the token
/// the service issued. A faked client would return whatever second page the author imagined and would prove
/// only that the fake agrees with the test.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbQueryPagingShould : IClassFixture<DynamoDbQueryPagingContainerFixture>, IAsyncLifetime
{
	private const int TotalDocuments = 5;
	private const int PageSize = 2;

	private readonly DynamoDbQueryPagingContainerFixture _fixture;

	// xUnit constructs a new instance per test method, so every arm seeds its own partition. A shared
	// partition would make the second arm's seeding collide with the first's, and — worse — would let one
	// arm's documents change another arm's page count.
	private readonly string _partition = $"tenant-{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbQueryPagingShould"/> class.
	/// </summary>
	/// <param name="fixture">The LocalStack DynamoDB fixture.</param>
	public DynamoDbQueryPagingShould(DynamoDbQueryPagingContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			$"LocalStack DynamoDB must be available for real-infra paging conformance (never skipped): {_fixture.InitializationError}");

		var provider = CreateProvider();
		for (var i = 0; i < TotalDocuments; i++)
		{
			var result = await provider.CreateAsync(
				new PagedDocument { Id = $"doc-{i:D2}", Name = $"document {i}" },
				new PartitionKey(_partition),
				CancellationToken.None).ConfigureAwait(false);

			result.Success.ShouldBeTrue($"seeding doc-{i:D2} must succeed: {result.ErrorMessage}");
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// SAFETY — the ruled arm. Every document is reachable by following the continuation token, and the
	/// second page is obtained through the public API using only the first page's token.
	/// </summary>
	[Fact]
	public async Task ReachEveryDocumentByFollowingTheContinuationToken()
	{
		var provider = CreateProvider();

		var seen = new List<string>();
		string? continuationToken = null;
		var pages = 0;

		do
		{
			var page = await provider.QueryAsync<PagedDocument>(
				new CloudQueryRequest
				{
					QueryText = "*",
					PartitionKey = new PartitionKey(_partition),
					MaxItemCount = PageSize,
					ContinuationToken = continuationToken,
				},
				CancellationToken.None).ConfigureAwait(false);

			pages++;
			seen.AddRange(page.Documents.Select(d => d.Id));
			continuationToken = page.ContinuationToken;

			// Guard the loop itself: a token that silently restarted at page one would spin forever, and
			// an unbounded test would hang the shard rather than fail it.
			pages.ShouldBeLessThanOrEqualTo(TotalDocuments + 1, "paging did not terminate");
		}
		while (!string.IsNullOrEmpty(continuationToken));

		// The whole point: nothing was lost and nothing was served twice.
		seen.Count.ShouldBe(TotalDocuments);
		seen.ShouldBeUnique();
		seen.Order().ShouldBe(Enumerable.Range(0, TotalDocuments).Select(i => $"doc-{i:D2}"));

		// And it genuinely paged — a provider that ignored MaxItemCount and returned everything at once
		// would satisfy every assertion above while proving nothing about the continuation.
		pages.ShouldBeGreaterThan(1, "the query must have been served in more than one page");
	}

	/// <summary>
	/// LIVENESS — a result set that fits in one page reports no continuation. Without this arm, a provider
	/// that always advertised another page would satisfy the safety arm while looping a caller forever.
	/// </summary>
	[Fact]
	public async Task ReportNoContinuationWhenTheResultSetFitsInOnePage()
	{
		var provider = CreateProvider();

		var page = await provider.QueryAsync<PagedDocument>(
			new CloudQueryRequest
			{
				QueryText = "*",
				PartitionKey = new PartitionKey(_partition),
				MaxItemCount = TotalDocuments + 10,
			},
			CancellationToken.None).ConfigureAwait(false);

		page.Documents.Count.ShouldBe(TotalDocuments);
		page.HasMoreResults.ShouldBeFalse();
		page.ContinuationToken.ShouldBeNull();
	}

	/// <summary>
	/// SAFETY — a token this provider did not issue is refused. Restarting at the first page instead would
	/// hand a paging loop the same page forever while appearing to advance.
	/// </summary>
	[Fact]
	public async Task RefuseAContinuationTokenItDidNotIssue()
	{
		var provider = CreateProvider();

		_ = await Should.ThrowAsync<ArgumentException>(() => provider.QueryAsync<PagedDocument>(
			new CloudQueryRequest
			{
				QueryText = "*",
				PartitionKey = new PartitionKey(_partition),
				ContinuationToken = "not-a-token-this-provider-issued",
			},
			CancellationToken.None)).ConfigureAwait(false);
	}

	private DynamoDbPersistenceProvider CreateProvider() =>
		new(
			_fixture.Client,
			Options.Create(new DynamoDbOptions
			{
				Name = "dynamodb-paging",
				DefaultTableName = _fixture.TableName,
			}),
			NullLogger<DynamoDbPersistenceProvider>.Instance);

	private sealed class PagedDocument
	{
		public string Id { get; init; } = string.Empty;

		public string Name { get; init; } = string.Empty;
	}
}
