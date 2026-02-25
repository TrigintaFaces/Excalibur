// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the configuration for an Elasticsearch component template.
/// </summary>
public sealed class ComponentTemplateConfiguration
{
	/// <summary>
	/// Gets the version of this component template.
	/// </summary>
	/// <value> The component template version number. </value>
	public long? Version { get; init; }

	/// <summary>
	/// Gets the template settings.
	/// </summary>
	/// <value> The index settings configuration. </value>
	public IndexSettings? Template { get; init; }

	/// <summary>
	/// Gets the field mappings for documents.
	/// </summary>
	/// <value> The type mapping configuration. </value>
	public TypeMapping? Mappings { get; init; }

	/// <summary>
	/// Gets custom metadata associated with this component template.
	/// </summary>
	/// <value> A dictionary of metadata key-value pairs. </value>
	public Dictionary<string, object?>? Metadata { get; init; }
}
