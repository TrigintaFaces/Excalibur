// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Excalibur.Data.ElasticSearch;
using ElasticSearch_Resilience.Domain;

namespace ElasticSearch_Resilience.Repositories;

public class OrderRepository : ElasticRepositoryBase<Order>, IOrderRepository
{
    public OrderRepository(ElasticsearchClient client)
        : base(client, "orders-resilience")
    {
    }

    public override Task InitializeAsync(CancellationToken cancellationToken)
        => InitializeIndexAsync(cancellationToken);

    public override async Task InitializeIndexAsync(CancellationToken cancellationToken)
    {
        var request = new CreateIndexRequest("orders-resilience")
        {
            Mappings = new TypeMapping
            {
                Properties = new Properties
                {
                    { "id", new KeywordProperty() },
                    { "customerId", new KeywordProperty() },
                    { "total", new FloatNumberProperty() },
                    { "status", new KeywordProperty() },
                    { "createdAt", new DateProperty() },
                }
            }
        };

        await InitializeIndexAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
