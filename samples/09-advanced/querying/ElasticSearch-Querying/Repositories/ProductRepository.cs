// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using ElasticSearchQuerying.Domain;

using Excalibur.Data.ElasticSearch;

namespace ElasticSearchQuerying.Repositories;

/// <summary>
/// Elasticsearch repository for products with custom index mappings
/// optimized for the querying patterns demonstrated in this sample.
/// </summary>
public sealed class ProductRepository : ElasticRepositoryBase<Product>, IProductRepository
{
    private const string IndexName = "products-querying";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductRepository"/> class.
    /// </summary>
    /// <param name="client">The Elasticsearch client.</param>
    public ProductRepository(ElasticsearchClient client)
        : base(client, IndexName)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates the "products-querying" index with explicit field mappings:
    /// <list type="bullet">
    ///   <item><c>name</c>, <c>description</c> - text (full-text search)</item>
    ///   <item><c>category</c>, <c>tags</c> - keyword (exact match, aggregations)</item>
    ///   <item><c>price</c>, <c>rating</c> - float (range queries)</item>
    ///   <item><c>stockQuantity</c> - integer (range queries)</item>
    ///   <item><c>createdAt</c> - date (date range queries)</item>
    /// </list>
    /// </remarks>
    public override Task InitializeIndexAsync(CancellationToken cancellationToken)
    {
        var createIndexRequest = new CreateIndexRequest(IndexName)
        {
            Mappings = new TypeMapping
            {
                Properties = new Properties
                {
                    ["id"] = new KeywordProperty(),
                    ["name"] = new TextProperty
                    {
                        Fields = new Properties
                        {
                            ["keyword"] = new KeywordProperty()
                        }
                    },
                    ["description"] = new TextProperty(),
                    ["category"] = new KeywordProperty(),
                    ["price"] = new FloatNumberProperty(),
                    ["stockQuantity"] = new IntegerNumberProperty(),
                    ["rating"] = new FloatNumberProperty(),
                    ["tags"] = new KeywordProperty(),
                    ["createdAt"] = new DateProperty()
                }
            }
        };

        return InitializeIndexAsync(createIndexRequest, cancellationToken);
    }
}
