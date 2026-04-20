// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Excalibur.Data.ElasticSearch;
using ElasticSearch_GettingStarted.Domain;

namespace ElasticSearch_GettingStarted.Repositories;

public class ProductRepository : ElasticRepositoryBase<Product>, IProductRepository
{
    public ProductRepository(ElasticsearchClient client)
        : base(client, "products")
    {
    }

    public override Task InitializeAsync(CancellationToken cancellationToken)
        => InitializeIndexAsync(cancellationToken);

    public override async Task InitializeIndexAsync(CancellationToken cancellationToken)
    {
        // Create index with explicit mappings for our Product document
        var request = new CreateIndexRequest("products")
        {
            Mappings = new TypeMapping
            {
                Properties = new Properties
                {
                    { "id", new KeywordProperty() },
                    { "name", new TextProperty { Fields = new Properties { { "keyword", new KeywordProperty() } } } },
                    { "category", new KeywordProperty() },
                    { "price", new FloatNumberProperty() },
                    { "stockQuantity", new IntegerNumberProperty() },
                    { "createdAt", new DateProperty() },
                }
            }
        };

        await InitializeIndexAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
