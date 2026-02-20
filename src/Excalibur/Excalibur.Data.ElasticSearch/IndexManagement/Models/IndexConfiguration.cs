// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the configuration for creating a new index.
/// </summary>
public sealed class IndexConfiguration
{
	/// <summary>
	/// Gets the index settings.
	/// </summary>
	/// <value> The settings configuration for the index. </value>
	public IndexSettings? Settings { get; init; }

	/// <summary>
	/// Gets the field mappings for documents in the index.
	/// </summary>
	/// <value> The type mapping configuration. </value>
	public TypeMapping? Mappings { get; init; }

	/// <summary>
	/// Gets the index aliases.
	/// </summary>
	/// <value> A dictionary of alias names and their configurations. </value>
	public Dictionary<string, Alias>? Aliases { get; init; }
}
