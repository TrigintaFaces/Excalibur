// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Dispatch;
using Excalibur.Outbox.ElasticSearch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Outbox;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Conformance tests for <see cref="ElasticsearchOutboxStore"/> using the Outbox Conformance Test Kit
/// against a real Elasticsearch container.
/// </summary>
/// <remarks>
/// These tests verify that the Elasticsearch implementation correctly implements the IOutboxStore
/// contract against real infrastructure (Elasticsearch via TestContainers), exercising the container-
/// connected <see cref="ElasticsearchClient"/> built with the SDK's default serializer settings.
/// </remarks>
[Collection(ElasticsearchOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "ElasticSearch")]
public sealed class ElasticsearchOutboxStoreConformanceShould : OutboxStoreConformanceTestBase, IClassFixture<ElasticsearchOutboxStoreContainerFixture>
{
	private readonly ElasticsearchOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Elasticsearch container fixture.</param>
	public ElasticsearchOutboxStoreConformanceShould(ElasticsearchOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Elasticsearch container must be available for real-infrastructure conformance — never skipped.");

		// "wait_for" refresh makes staged/updated documents immediately searchable, which the conformance
		// kit relies on for read-after-write assertions.
		var options = Options.Create(new ElasticsearchOutboxOptions
		{
			IndexName = _fixture.IndexName,
			RefreshPolicy = "wait_for",
		});

		var store = new ElasticsearchOutboxStore(
			_fixture.Client,
			options,
			NullLogger<ElasticsearchOutboxStore>.Instance);

		return Task.FromResult<IOutboxStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.DeleteIndexAsync().ConfigureAwait(false);
	}
}
