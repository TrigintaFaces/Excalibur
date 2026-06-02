// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the configuration for creating a new index.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="SettingsJson"/>, <see cref="MappingsJson"/>, and <see cref="AliasesJson"/>
/// properties accept opaque JSON payloads so that consumers are not coupled to a specific
/// Elasticsearch SDK version. Consumers who need typed configuration can serialize SDK types
/// to <see cref="JsonElement"/> before assigning them:
/// </para>
/// <code>
/// var settings = new IndexSettings { NumberOfShards = 1 };
/// var mappings = new TypeMapping { Properties = new Properties { ... } };
/// var config = new IndexConfiguration
/// {
///     SettingsJson = JsonSerializer.SerializeToElement(settings),
///     MappingsJson = JsonSerializer.SerializeToElement(mappings),
/// };
/// </code>
/// </remarks>
public sealed class IndexConfiguration
{
	/// <summary>
	/// Gets the index settings as opaque JSON.
	/// </summary>
	/// <value> The settings configuration as a JSON element, or <see langword="null"/> if not specified. </value>
	public JsonElement? SettingsJson { get; init; }

	/// <summary>
	/// Gets the field mappings for documents in the index as opaque JSON.
	/// </summary>
	/// <value> The type mapping configuration as a JSON element, or <see langword="null"/> if not specified. </value>
	public JsonElement? MappingsJson { get; init; }

	/// <summary>
	/// Gets the index aliases as opaque JSON.
	/// </summary>
	/// <value> A JSON element representing alias configurations, or <see langword="null"/> if not specified. </value>
	public JsonElement? AliasesJson { get; init; }
}
