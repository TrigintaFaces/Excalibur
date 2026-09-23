// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net.Http.Json;

using Elastic.Clients.Elasticsearch;

using Excalibur.Dispatch;
using Excalibur.Inbox.ElasticSearch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;
using Excalibur.Data.ElasticSearch.Persistence;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Conformance tests for <see cref="ElasticsearchInboxStore"/> using the Inbox Conformance Test Kit
/// against a real Elasticsearch container.
/// </summary>
/// <remarks>
/// These tests verify that the Elasticsearch implementation correctly implements the IInboxStore
/// contract against real infrastructure (Elasticsearch via TestContainers), exercising the container-
/// connected <see cref="ElasticsearchClient"/> built with the SDK's default serializer settings.
/// </remarks>
[Collection(ElasticsearchInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "ElasticSearch")]
public sealed class ElasticsearchInboxStoreConformanceShould : InboxStoreConformanceTestBase, IClassFixture<ElasticsearchInboxStoreContainerFixture>
{
	private readonly ElasticsearchInboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Elasticsearch container fixture.</param>
	public ElasticsearchInboxStoreConformanceShould(ElasticsearchInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>The same context <see cref="CreateStoreAsync"/> hands the store.</remarks>
	protected override ITenantContext StoreTenantContext => SingleTenantTestContext.Instance;

	/// <inheritdoc/>
	protected override Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Elasticsearch container must be available for real-infrastructure conformance — never skipped.");

		// "wait_for" refresh makes staged/updated documents immediately searchable, which the conformance
		// kit relies on for read-after-write assertions.
		var options = Options.Create(new ElasticsearchInboxOptions
		{
			IndexName = _fixture.IndexName,
			RefreshPolicy = ElasticsearchRefreshPolicy.WaitFor,
		});

		var store = new ElasticsearchInboxStore(
			_fixture.Client,
			options,
			NullLogger<ElasticsearchInboxStore>.Instance,
			SingleTenantTestContext.Instance);

		return Task.FromResult<IInboxStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.DeleteIndexAsync().ConfigureAwait(false);
	}

	// 47ruyr (2mek4x follow-up): a real, provider-side persistence rejection -- never a mocked client.
	// index.blocks.write is Elasticsearch's own mechanism for making an index reject writes; the typed
	// client has no strongly-typed model for it (the framework's own IndexOperationsManager takes raw
	// settings JSON for the same reason), so this goes over plain HTTP against the fixture's container.
	private static async Task PutIndexWriteBlockAsync(string url, string indexName, bool blocked)
	{
		using var http = new HttpClient();
		using var content = JsonContent.Create(new { index = new { blocks = new { write = blocked } } });
		using var response = await http.PutAsync(new Uri($"{url.TrimEnd('/')}/{indexName}/_settings"), content).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			throw new HttpRequestException(
				$"PUT {indexName}/_settings (blocks.write={blocked}) -> {(int)response.StatusCode} {response.StatusCode}: {body}");
		}
	}

	/// <inheritdoc/>
	protected override async Task InjectPersistenceFaultAsync()
	{
		// The store auto-creates the index lazily on first write, so this fault-injection test may run
		// before any other operation has touched it -- create it now (best effort; the store's own create
		// wins the race harmlessly if it gets there first) before blocking writes to it.
		_ = await _fixture.Client.Indices.CreateAsync(_fixture.IndexName).ConfigureAwait(false);

		await PutIndexWriteBlockAsync(_fixture.Url, _fixture.IndexName, blocked: true).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task RemovePersistenceFaultAsync() =>
		await PutIndexWriteBlockAsync(_fixture.Url, _fixture.IndexName, blocked: false).ConfigureAwait(false);
}
