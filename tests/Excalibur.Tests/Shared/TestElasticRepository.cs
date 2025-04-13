using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.DataAccess.ElasticSearch;
using Excalibur.Tests.Mothers;

namespace Excalibur.Tests.Shared;

public interface ITestElasticRepository : IElasticRepositoryBase<TestElasticDocument>
{
}

public sealed class TestElasticRepository(ElasticsearchClient client)
	: ElasticRepositoryBase<TestElasticDocument>(client, IndexName), ITestElasticRepository
{
	private const string IndexName = "test-elastic-index";

	public override async Task InitializeIndexAsync(CancellationToken cancellationToken = default)
	{
		var request = new CreateIndexRequest(IndexName)
		{
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
			Mappings = new TypeMapping
			{
				Properties = new Properties
				{
					{ Infer.Property<TestElasticDocument>(x => x.Id), new KeywordProperty() },
					{ Infer.Property<TestElasticDocument>(x => x.Name), new TextProperty() }
				}
			}
		};

		await InitializeIndexAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
