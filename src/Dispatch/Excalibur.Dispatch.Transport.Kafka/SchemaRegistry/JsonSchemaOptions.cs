// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Options for JSON Schema generation.
/// </summary>
/// <remarks>
/// <para>
/// These options control how JSON Schemas are generated from .NET types,
/// including support for custom schema annotations.
/// </para>
/// </remarks>
public sealed class JsonSchemaOptions
{
	/// <summary>
	/// Gets or sets whether to include schema annotations from attributes.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to process <see cref="SchemaDescriptionAttribute"/>,
	/// <see cref="SchemaExampleAttribute"/>, and <see cref="SchemaDeprecatedAttribute"/>;
	/// otherwise, <see langword="false"/>. Default is <see langword="false"/>.
	/// </value>
	public bool IncludeAnnotations { get; set; }

	/// <summary>
	/// Gets or sets whether to allow additional properties in object schemas.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to allow additional properties not defined in the schema;
	/// otherwise, <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	public bool AllowAdditionalProperties { get; set; } = true;

	/// <summary>
	/// Gets or sets custom JSON serializer options for schema generation.
	/// </summary>
	/// <value>
	/// Custom <see cref="JsonSerializerOptions"/> to use, or <see langword="null"/>
	/// to use the default options (camelCase, no indentation).
	/// </value>
	public JsonSerializerOptions? JsonSerializerOptions { get; set; }

	/// <summary>
	/// Gets or sets whether to treat null-oblivious reference types as non-nullable.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to treat null-oblivious types as non-nullable;
	/// otherwise, <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	public bool TreatNullObliviousAsNonNullable { get; set; } = true;
}
