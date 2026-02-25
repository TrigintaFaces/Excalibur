// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Metadata about a registered schema.
/// </summary>
public sealed class SchemaMetadata
{
	/// <summary>
	/// Gets or sets the type name.
	/// </summary>
	/// <value>
	/// The type name.
	/// </value>
	public string TypeName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema definition.
	/// </summary>
	/// <value>
	/// The schema definition.
	/// </value>
	public string Schema { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema version.
	/// </summary>
	/// <value>
	/// The schema version.
	/// </value>
	public int Version { get; set; }

	/// <summary>
	/// Gets or sets the serialization format.
	/// </summary>
	/// <value>
	/// The serialization format.
	/// </value>
	public SerializationFormat Format { get; set; }

	/// <summary>
	/// Gets or sets when the schema was registered.
	/// </summary>
	/// <value>
	/// When the schema was registered.
	/// </value>
	public DateTimeOffset RegisteredAt { get; set; }

	/// <summary>
	/// Gets or sets additional metadata.
	/// </summary>
	/// <value>
	/// Additional metadata.
	/// </value>
	public Dictionary<string, string> Metadata { get; set; } = [];
}
