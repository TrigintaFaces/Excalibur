// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the configuration for an Elasticsearch component template.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="SettingsJson"/> and <see cref="MappingsJson"/> properties accept
/// opaque JSON payloads so that consumers are not coupled to a specific Elasticsearch
/// SDK version.
/// </para>
/// </remarks>
public sealed class ComponentTemplateConfiguration
{
	/// <summary>
	/// Gets the version of this component template.
	/// </summary>
	/// <value> The component template version number. </value>
	public long? Version { get; init; }

	/// <summary>
	/// Gets the template settings as opaque JSON.
	/// </summary>
	/// <value> The index settings configuration as a JSON element, or <see langword="null"/> if not specified. </value>
	public JsonElement? SettingsJson { get; init; }

	/// <summary>
	/// Gets the field mappings as opaque JSON.
	/// </summary>
	/// <value> The type mapping configuration as a JSON element, or <see langword="null"/> if not specified. </value>
	public JsonElement? MappingsJson { get; init; }

	/// <summary>
	/// Gets custom metadata associated with this component template.
	/// </summary>
	/// <value> A dictionary of metadata key-value pairs. </value>
	public Dictionary<string, object?>? Metadata { get; init; }
}
