// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

namespace Excalibur.Data.OpenSearch.IndexManagement;

/// <summary>
/// Represents the configuration for an OpenSearch index template.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="SettingsJson"/> and <see cref="MappingsJson"/> properties accept
/// opaque JSON payloads so that consumers are not coupled to a specific OpenSearch
/// SDK version. Consumers who need typed configuration can serialize SDK types to
/// <see cref="JsonElement"/> before assigning them:
/// </para>
/// <code>
/// var settings = new IndexSettings { NumberOfShards = 1 };
/// var config = new IndexTemplateConfiguration
/// {
///     IndexPatterns = ["logs-*"],
///     SettingsJson = JsonSerializer.SerializeToElement(settings),
/// };
/// </code>
/// </remarks>
public sealed class IndexTemplateConfiguration
{
	/// <summary>
	/// Gets the index patterns that this template applies to.
	/// </summary>
	/// <value> A collection of index patterns (e.g., "logs-*", "metrics-*"). </value>
	public required IEnumerable<string> IndexPatterns { get; init; }

	/// <summary>
	/// Gets the priority of this template when multiple templates match an index.
	/// </summary>
	/// <value> Higher values take precedence. Defaults to 100. </value>
	public int Priority { get; init; } = 100;

	/// <summary>
	/// Gets the version of this template for tracking changes.
	/// </summary>
	/// <value> The template version number. </value>
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
	/// Gets the component templates to be composed into this template.
	/// </summary>
	/// <value> A collection of component template names. </value>
	public IEnumerable<string>? ComposedOf { get; init; }

	/// <summary>
	/// Gets the data stream configuration if this template creates data streams.
	/// </summary>
	/// <value> The data stream configuration. </value>
	public DataStreamConfiguration? DataStream { get; init; }

	/// <summary>
	/// Gets custom metadata associated with this template.
	/// </summary>
	/// <value> A dictionary of metadata key-value pairs. </value>
	public Dictionary<string, object?>? Metadata { get; init; }

	/// <summary>
	/// Gets a value indicating whether this template should allow auto-creation of indices.
	/// </summary>
	/// <value> True to allow auto-creation, false otherwise. Defaults to true. </value>
	public bool AllowAutoCreate { get; init; } = true;
}
