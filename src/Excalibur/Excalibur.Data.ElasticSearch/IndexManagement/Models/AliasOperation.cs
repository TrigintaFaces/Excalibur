// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents an alias operation to be performed atomically.
/// </summary>
public sealed class AliasOperation
{
	/// <summary>
	/// Gets the type of alias operation.
	/// </summary>
	/// <value> The operation type (add, remove). </value>
	public required AliasOperationType OperationType { get; init; }

	/// <summary>
	/// Gets the name of the alias.
	/// </summary>
	/// <value> The alias name. </value>
	public required string AliasName { get; init; }

	/// <summary>
	/// Gets the name of the index.
	/// </summary>
	/// <value> The index name. </value>
	public required string IndexName { get; init; }

	/// <summary>
	/// Gets the alias configuration for add operations.
	/// </summary>
	/// <value> The alias configuration. </value>
	public Alias? AliasConfiguration { get; init; }
}
