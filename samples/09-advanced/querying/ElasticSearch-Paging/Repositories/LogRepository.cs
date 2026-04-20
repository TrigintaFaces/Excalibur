// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Excalibur.Data.ElasticSearch;
using ElasticSearch_Paging.Domain;

namespace ElasticSearch_Paging.Repositories;

public class LogRepository : ElasticRepositoryBase<LogEntry>, ILogRepository
{
    public LogRepository(ElasticsearchClient client)
        : base(client, "logs-paging")
    {
    }

    public override Task InitializeAsync(CancellationToken cancellationToken)
        => InitializeIndexAsync(cancellationToken);

    public override async Task InitializeIndexAsync(CancellationToken cancellationToken)
    {
        var request = new CreateIndexRequest("logs-paging")
        {
            Mappings = new TypeMapping
            {
                Properties = new Properties
                {
                    { "id", new KeywordProperty() },
                    { "timestamp", new DateProperty() },
                    { "level", new KeywordProperty() },
                    { "message", new TextProperty() },
                    { "service", new KeywordProperty() },
                    { "traceId", new KeywordProperty() },
                }
            }
        };

        await InitializeIndexAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
