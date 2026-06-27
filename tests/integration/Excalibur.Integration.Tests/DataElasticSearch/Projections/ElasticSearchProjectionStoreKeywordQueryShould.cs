// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.DataElasticSearch.Projections;

/// <summary>
/// bd-5jo6tm-B (S853 review P3) — independent regression lock (author≠impl, TestsDeveloper) for
/// <c>ElasticSearchProjectionStore&lt;T&gt;</c> exact-match query field naming against a real
/// Elasticsearch (TestContainers).
/// <para>
/// SA option B: the exact-match (<c>.keyword</c>) decision is derived from the DECLARED ES mapping
/// (<see cref="ElasticIndexMappingBuilder.BuildMappingProperties{T}"/> /
/// <c>ClassifyDeclaredField</c>) — a string mapped as <c>keyword</c> (the inferred default) is already
/// exact-match and is queried/sorted as-is; only an analyzed <c>text</c> field needs the
/// <c>.keyword</c> sub-field. The pre-fix code ALWAYS appended <c>.keyword</c>, so an exact-match
/// filter (and the sort) on a default keyword-mapped string field targeted a NON-EXISTENT
/// <c>name.keyword</c> / <c>status.keyword</c> sub-field and silently matched nothing.
/// </para>
/// <para>
/// Non-vacuity (RED on the pre-fix code): two documents are indexed (A=active, B=closed) into an index
/// whose mapping classifies the string fields as <c>keyword</c>. A filter (<c>status == active</c>)
/// plus a sort (<c>OrderBy name</c>) on those keyword fields must return exactly document A. Against
/// the pre-fix impl the filter resolves to <c>status.keyword</c> (unmapped on a keyword field) → zero
/// matches (and the sort on <c>name.keyword</c> errors), so the query returns 0 docs (or throws) → RED.
/// The fixed impl resolves to the bare field names → returns exactly A → GREEN.
/// </para>
/// <para>
/// OpenSearch-vs-ES note: bd-5jo6tm frames this as an "OpenSearch <c>.keyword</c>" symptom, but the
/// SA fix and the grounded seam live in <c>Excalibur.Data.ElasticSearch</c>
/// (<c>ElasticSearchProjectionStore&lt;T&gt;</c>, <c>Elastic.Clients.Elasticsearch</c>). The
/// mapping-derivation logic (declared-mapping → exact-match treatment) is engine-agnostic; this lock
/// exercises it through the real Elasticsearch fixture (<see cref="ElasticsearchIntegrationTestBase"/>),
/// which is the harness the fixed seam runs against. The sibling OpenSearch store has its own real-
/// OpenSearch projection-store integration test.
/// </para>
/// <para>
/// Non-skip: this is a behavioral correctness lock; <see cref="ElasticsearchIntegrationTestBase"/>
/// structurally REQUIRES the real container (it throws on Docker-unavailable rather than skipping), so
/// the lock can never silently pass by being skipped. The base does not expose a <c>DockerAvailable</c>
/// property; the explicit <c>Client.ShouldNotBeNull</c> guard documents the never-skipped intent. Run
/// serially (<c>-m:1</c>).
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Projections")]
public sealed class ElasticSearchProjectionStoreKeywordQueryShould : ElasticsearchIntegrationTestBase
{
	[Fact]
	public async Task ResolveExactMatchFilterAndSortAgainstDeclaredKeywordMapping_NotANonexistentKeywordSubField()
	{
		// Never-skipped: the real ES client must be live (the base throws if Docker is unavailable).
		Client.ShouldNotBeNull(
			"5jo6tm-B exact-match query correctness is a real-ES behavioral lock — it must never be skipped");

		// Arrange — a projection whose string fields (Name, Status) infer to `keyword` mappings (the
		// default in ElasticIndexMappingBuilder). Unique status values isolate this run from any other
		// docs the shared container might hold.
		var activeStatus = $"active-{Guid.NewGuid():N}";
		var closedStatus = $"closed-{Guid.NewGuid():N}";

		var options = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = ConnectionString,
			IndexPrefix = $"kwq-{Guid.NewGuid():N}",
			CreateIndexOnInitialize = true,
		};

		// Register the index for base-class cleanup.
		var indexName = ElasticSearchProjectionIndexConvention.GetIndexName<KeywordQueryProjection>(options);
		CreatedIndices.Add(indexName);

		await using var store = new ElasticSearchProjectionStore<KeywordQueryProjection>(
			Client,
			CreateOptionsMonitor(options),
			NullLogger<ElasticSearchProjectionStore<KeywordQueryProjection>>.Instance);

		await store.UpsertAsync(
			"kwq-a",
			new KeywordQueryProjection { Id = "kwq-a", Name = "Alpha", Status = activeStatus },
			CancellationToken.None).ConfigureAwait(false);
		await store.UpsertAsync(
			"kwq-b",
			new KeywordQueryProjection { Id = "kwq-b", Name = "Beta", Status = closedStatus },
			CancellationToken.None).ConfigureAwait(false);

		// Deterministic visibility: refresh the index instead of sleeping (testing-patterns determinism).
		_ = await Client.Indices.RefreshAsync(indexName).ConfigureAwait(false);

		// Act — exact-match filter on the keyword-mapped `status` field, plus a sort on the keyword-mapped
		// `name` field. Both must resolve to the BARE field names (declared as `keyword`), not `*.keyword`.
		var filters = new Dictionary<string, object>(StringComparer.Ordinal) { ["status"] = activeStatus };
		var queryOptions = new QueryOptions(OrderBy: nameof(KeywordQueryProjection.Name), Descending: false);

		var results = await store.QueryAsync(filters, queryOptions, CancellationToken.None).ConfigureAwait(false);

		// Assert — exactly the matching document is returned. RED on the pre-fix impl: the filter targets
		// the non-existent `status.keyword` sub-field on a direct-keyword field → 0 matches (and the sort
		// on the non-existent `name.keyword` errors the search).
		results.Count.ShouldBe(
			1,
			"the exact-match filter on a declared-keyword field must resolve to the bare field name and match the indexed document");
		results[0].Id.ShouldBe("kwq-a");
		results[0].Status.ShouldBe(activeStatus);
	}

	// Minimal IOptionsMonitor double: the store resolves named options by typeof(TProjection).Name;
	// returning the same configured options for any name is sufficient for this single-type test.
	private static IOptionsMonitor<ElasticSearchProjectionStoreOptions> CreateOptionsMonitor(
		ElasticSearchProjectionStoreOptions options)
	{
		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(options);
		return monitor;
	}

	// String fields (Name, Status) infer to `keyword` ES mappings — the exact scenario the pre-fix code
	// mis-queried by always appending `.keyword`.
	private sealed class KeywordQueryProjection
	{
		public string Id { get; init; } = string.Empty;

		public string Name { get; init; } = string.Empty;

		public string Status { get; init; } = string.Empty;
	}
}
