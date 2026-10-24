// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>Captures the destination scope alongside the shared client that actually uses it.</summary>
internal sealed record ConfiguredProjectionClient(ElasticsearchClient Client, string Scope, System.Text.Json.Serialization.JsonSerializerContext? SourceContext = null);
