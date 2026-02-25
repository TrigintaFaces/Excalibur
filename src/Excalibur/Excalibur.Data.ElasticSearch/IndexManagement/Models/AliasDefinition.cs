// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents an alias definition with its associated indices.
/// </summary>
public sealed class AliasDefinition
{
	/// <summary>
	/// Gets the name of the alias.
	/// </summary>
	/// <value> The alias name. </value>
	public required string AliasName { get; init; }

	/// <summary>
	/// Gets the indices associated with this alias.
	/// </summary>
	/// <value> A collection of index names. </value>
	public required IEnumerable<string> Indices { get; init; }

	/// <summary>
	/// Gets or sets the filter query for the alias.
	/// </summary>
	/// <value> The filter query. </value>
	public Query? Filter { get; set; }

	/// <summary>
	/// Gets or sets the index routing value.
	/// </summary>
	/// <value> The index routing. </value>
	public string? IndexRouting { get; set; }

	/// <summary>
	/// Gets or sets the search routing value.
	/// </summary>
	/// <value> The search routing. </value>
	public string? SearchRouting { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this alias is a write index.
	/// </summary>
	/// <value> True if this is a write index; otherwise, false. </value>
	public bool? IsWriteIndex { get; set; }
}
