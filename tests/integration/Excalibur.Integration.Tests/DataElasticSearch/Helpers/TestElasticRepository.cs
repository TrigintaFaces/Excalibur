// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Integration.Tests.DataElasticSearch.Helpers;

public sealed class TestElasticRepository(ElasticsearchClient client)
	: ElasticRepositoryBase<TestElasticDocument>(client, IndexName), ITestElasticRepository
{
	private const string IndexName = "test-elastic-index";

	/// <inheritdoc/>
	public override async Task InitializeIndexAsync(CancellationToken cancellationToken = default)
	{
		var request = new CreateIndexRequest(IndexName)
		{
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
			Mappings = new TypeMapping
			{
				Properties = new Properties
				{
					{ Infer.Property<TestElasticDocument>(static x => x.Id), new KeywordProperty() },
					{ Infer.Property<TestElasticDocument>(static x => x.Name), new TextProperty() },
				},
			},
		};

		await InitializeIndexAsync(request, cancellationToken).ConfigureAwait(true);
	}
}
