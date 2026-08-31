// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Data.ElasticSearch.Persistence;
using Excalibur.Data.OpenSearch.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Logging.Abstractions;

using OpenSearch.Client;
using OpenSearch.Net;

namespace Excalibur.Data.Tests.Search;

/// <summary>
/// Binds the search providers' reported availability to a cluster they actually reached.
/// </summary>
/// <remarks>
/// <para>
/// Both providers previously set their initialized flag with no I/O whatsoever, so
/// <c>IsAvailable</c> was true against an unreachable cluster. Each provider is asserted in a pair: the
/// safety arm proves an unreachable cluster is reported as such, and the liveness arm proves a
/// reachable one still initializes -- a provider hard-wired to report unavailable would satisfy the
/// safety arm perfectly and is exactly what the liveness arm exists to exclude.
/// </para>
/// <para>
/// Both clients are driven by their own in-memory transport, so these are deterministic and reach no
/// network.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SearchProviderInitializeReachabilityShould : UnitTestBase
{
	private static readonly Uri Node = new("http://localhost:9200");

	/// <summary>
	/// The header a real Elasticsearch cluster returns to identify itself.
	/// </summary>
	/// <remarks>
	/// The Elastic v8+ client runs a product check on its first request and throws
	/// <c>UnsupportedProductException</c> for any response that does not carry this header -- before the
	/// status code is ever considered. Answering without it makes both arms below test the wrong thing:
	/// the 200 arm fails despite a reachable cluster, and the 500 arm passes for the product check
	/// rather than for the status code it names. The mock therefore answers the way a real cluster does,
	/// so each arm exercises the status code it is written for.
	/// </remarks>
	private static readonly Dictionary<string, IEnumerable<string>> ElasticProductHeader =
		new(StringComparer.OrdinalIgnoreCase) { ["x-elastic-product"] = ["Elasticsearch"] };

	private static byte[] Body(string json) => Encoding.UTF8.GetBytes(json);

	private static ElasticsearchClient ElasticClient(int statusCode) =>
		new(new ElasticsearchClientSettings(
			new Elastic.Transport.SingleNodePool(Node),
			new Elastic.Transport.InMemoryRequestInvoker(
				Body("{}"),
				statusCode,
				exception: null,
				contentType: "application/json",
				headers: ElasticProductHeader)));

	private static OpenSearchClient OpenSearchClientWith(int statusCode) =>
		new(new ConnectionSettings(
			new SingleNodeConnectionPool(Node),
			new InMemoryConnection(Body("{}"), statusCode)));

	private static IPersistenceOptions AnyPersistenceOptions() => A.Fake<IPersistenceOptions>();

	private static ElasticsearchPersistenceProvider Elastic(int statusCode) =>
		new(ElasticClient(statusCode),
			Options.Create(new ElasticsearchPersistenceOptions()),
			NullLogger<ElasticsearchPersistenceProvider>.Instance);

	private static OpenSearchPersistenceProvider Open(int statusCode) =>
		new(OpenSearchClientWith(statusCode),
			Options.Create(new OpenSearchPersistenceOptions()),
			NullLogger<OpenSearchPersistenceProvider>.Instance);

	// ---- Elasticsearch ----

	[Fact]
	public async Task Elasticsearch_NotReportAvailable_WhenTheClusterIsUnreachable()
	{
		await using var provider = Elastic(statusCode: 500);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => provider.InitializeAsync(AnyPersistenceOptions(), CancellationToken.None));

		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public async Task Elasticsearch_ReportAvailable_WhenTheClusterAnswers()
	{
		await using var provider = Elastic(statusCode: 200);

		provider.IsAvailable.ShouldBeFalse("not available before initialization");

		await provider.InitializeAsync(AnyPersistenceOptions(), CancellationToken.None).ConfigureAwait(false);

		provider.IsAvailable.ShouldBeTrue();
	}

	// ---- OpenSearch ----

	[Fact]
	public async Task OpenSearch_NotReportAvailable_WhenTheClusterIsUnreachable()
	{
		await using var provider = Open(statusCode: 500);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => provider.InitializeAsync(AnyPersistenceOptions(), CancellationToken.None));

		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public async Task OpenSearch_ReportAvailable_WhenTheClusterAnswers()
	{
		await using var provider = Open(statusCode: 200);

		provider.IsAvailable.ShouldBeFalse("not available before initialization");

		await provider.InitializeAsync(AnyPersistenceOptions(), CancellationToken.None).ConfigureAwait(false);

		provider.IsAvailable.ShouldBeTrue();
	}
}
