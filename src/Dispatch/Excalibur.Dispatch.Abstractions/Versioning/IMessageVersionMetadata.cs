// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines metadata for message versioning, including schema, serializer, and general version information.
/// </summary>
public interface IMessageVersionMetadata
{
	/// <summary>
	/// Gets or sets the schema version for migration logic and compatibility checks.
	/// </summary>
	int SchemaVersion { get; set; }

	/// <summary>
	/// Gets or sets the serializer or format version for serialization compatibility.
	/// </summary>
	int SerializerVersion { get; set; }

	/// <summary>
	/// Gets or sets the general version property for the message.
	/// </summary>
	int Version { get; set; }
}
