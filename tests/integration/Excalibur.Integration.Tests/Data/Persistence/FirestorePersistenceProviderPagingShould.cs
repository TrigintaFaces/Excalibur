// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CloudNative;
using Excalibur.Data.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Binds the rule that this provider pages like the others, so a caller holding the interface does not
/// have to know which provider it has.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks:</b> the provider threw <c>NotSupportedException</c> when
/// <see cref="CloudQueryRequest.MaxItemCount"/> was supplied, on the stated grounds that it could not
/// page. Cosmos DB and DynamoDB honour the same option, so a caller that set a page size purely to bound
/// its own memory worked against two providers and threw against the third — and there was no operation
/// on the interface by which it could ask which it had. That is a substitutability break, and the
/// interface contract had been weakened to make it look like a capability boundary.
/// </para>
/// <para>
/// <b>The premise was also false.</b> The SDK exposes <c>Query.Limit</c>, <c>Query.OrderBy</c> and
/// <c>Query.StartAfter</c>, and <c>Limit</c> was already being called elsewhere in the same file. The
/// provider could always have paged; only this method did not.
/// </para>
/// <para>
/// <b>Why a real emulator.</b> Ordering and cursor resumption are executed by the database. A fake would
/// return whatever sequence the test arranged and could not show that the cursor we hand back is one the
/// service accepts, nor that the ordering the page boundary depends on is the ordering it applies.
/// </para>
/// </remarks>
[Collection(FirestorePersistenceProviderTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
public sealed class FirestorePersistenceProviderPagingShould : IAsyncLifetime
{
	private const int TotalDocuments = 5;
	private const int PageSize = 2;

	private readonly FirestorePersistenceProviderContainerFixture _fixture;
	private readonly string _collection = $"paging_{Guid.NewGuid():N}";
	private readonly IPartitionKey _partitionKey = new PartitionKey("paging-tenant");

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestorePersistenceProviderPagingShould"/> class.
	/// </summary>
	/// <param name="fixture">The Firestore emulator container fixture.</param>
	public FirestorePersistenceProviderPagingShould(FirestorePersistenceProviderContainerFixture fixture) =>
		_fixture = fixture;

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator container must be available - this lock is never skipped, because a paging "
			+ "defect is invisible without one.");

		var provider = CreateProvider();

		// Ids are assigned so that DOCUMENT-ID order is well defined and known: paging is ordered by
		// document id, so the expected page contents are exactly the first N by that order.
		for (var i = 0; i < TotalDocuments; i++)
		{
			var result = await provider.CreateAsync(
				new PagedDocument { Id = $"doc-{i:D2}", Name = $"document {i}" },
				_partitionKey,
				CancellationToken.None).ConfigureAwait(false);

			result.Success.ShouldBeTrue($"seeding doc-{i:D2} must succeed: {result.ErrorMessage}");
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// SAFETY — a page size is HONOURED rather than refused, and the page is the size that was asked for.
	/// RED against the previous implementation, which threw <c>NotSupportedException</c>.
	/// </summary>
	[Fact]
	public async Task HonourAPageSizeInsteadOfRefusingIt()
	{
		var provider = CreateProvider();

		var page = await provider.QueryAsync<PagedDocument>(
			new CloudQueryRequest
			{
				QueryText = "*",
				PartitionKey = _partitionKey,
				MaxItemCount = PageSize,
			},
			CancellationToken.None).ConfigureAwait(false);

		page.Documents.Count.ShouldBe(PageSize);
		page.HasMoreResults.ShouldBeTrue("five documents do not fit in a page of two");
		page.ContinuationToken.ShouldNotBeNullOrEmpty();
	}

	/// <summary>
	/// SAFETY — the ruled arm. Every document is reachable by following the continuation, with nothing
	/// lost and nothing served twice.
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
					PartitionKey = _partitionKey,
					MaxItemCount = PageSize,
					ContinuationToken = continuationToken,
				},
				CancellationToken.None).ConfigureAwait(false);

			pages++;
			seen.AddRange(page.Documents.Select(d => d.Id));
			continuationToken = page.ContinuationToken;

			// A token that silently restarted would spin forever; an unbounded loop hangs the shard
			// instead of failing it.
			pages.ShouldBeLessThanOrEqualTo(TotalDocuments + 1, "paging did not terminate");
		}
		while (!string.IsNullOrEmpty(continuationToken));

		seen.Count.ShouldBe(TotalDocuments);
		seen.ShouldBeUnique();
		seen.Order().ShouldBe(Enumerable.Range(0, TotalDocuments).Select(i => $"doc-{i:D2}"));
		pages.ShouldBeGreaterThan(1, "the query must have been served in more than one page");
	}

	/// <summary>
	/// LIVENESS — a result set that fits in one page reports NO continuation. Without this arm, a provider
	/// that always advertised another page would satisfy the arms above and loop a caller forever.
	/// </summary>
	[Fact]
	public async Task ReportNoContinuationWhenTheResultSetFitsInOnePage()
	{
		var provider = CreateProvider();

		var page = await provider.QueryAsync<PagedDocument>(
			new CloudQueryRequest
			{
				QueryText = "*",
				PartitionKey = _partitionKey,
				MaxItemCount = TotalDocuments + 10,
			},
			CancellationToken.None).ConfigureAwait(false);

		page.Documents.Count.ShouldBe(TotalDocuments);
		page.HasMoreResults.ShouldBeFalse();
		page.ContinuationToken.ShouldBeNull();
	}

	/// <summary>
	/// LIVENESS — a query with no page size still returns everything, so adding paging did not make the
	/// unbounded read a special case.
	/// </summary>
	[Fact]
	public async Task ReturnEveryDocumentWhenNoPageSizeIsSupplied()
	{
		var provider = CreateProvider();

		var page = await provider.QueryAsync<PagedDocument>(
			new CloudQueryRequest { QueryText = "*", PartitionKey = _partitionKey },
			CancellationToken.None).ConfigureAwait(false);

		page.Documents.Count.ShouldBe(TotalDocuments);
		page.HasMoreResults.ShouldBeFalse();
	}

	/// <summary>
	/// SAFETY — a token this provider did not issue is REFUSED, not misread as a document id. Misreading
	/// it would resume from a position that does not exist and return an empty page that reads as the end
	/// of the data.
	/// </summary>
	[Fact]
	public async Task RefuseAContinuationTokenItDidNotIssue()
	{
		var provider = CreateProvider();

		_ = await Should.ThrowAsync<ArgumentException>(() => provider.QueryAsync<PagedDocument>(
			new CloudQueryRequest
			{
				QueryText = "*",
				PartitionKey = _partitionKey,
				ContinuationToken = "ddbstreams:1:not-a-firestore-token",
			},
			CancellationToken.None)).ConfigureAwait(false);
	}

	private FirestorePersistenceProvider CreateProvider() =>
		new(
			Options.Create(new FirestoreOptions
			{
				Name = "firestore-paging",
				ProjectId = _fixture.ProjectId,
				EmulatorHost = _fixture.EmulatorEndpoint,
				DefaultCollection = _collection,
			}),
			NullLogger<FirestorePersistenceProvider>.Instance);

	private sealed class PagedDocument
	{
		public string Id { get; init; } = string.Empty;

		public string Name { get; init; } = string.Empty;
	}
}
