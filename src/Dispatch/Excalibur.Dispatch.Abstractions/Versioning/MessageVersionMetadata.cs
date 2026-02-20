// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides concrete implementation of message version metadata containing schema, serializer, and general version information.
/// </summary>
public sealed class MessageVersionMetadata : IMessageVersionMetadata
{
	/// <summary>
	/// The metadata key used to identify message version metadata in message headers or metadata collections.
	/// </summary>
	public const string MetadataKey = "Excalibur.MessageVersionMetadata";

	/// <summary>
	/// Gets or sets the schema version for migration logic and compatibility checks.
	/// </summary>
	/// <value>The current <see cref="SchemaVersion"/> value.</value>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>
	/// Gets or sets the serializer or format version for serialization compatibility.
	/// </summary>
	/// <value>The current <see cref="SerializerVersion"/> value.</value>
	public int SerializerVersion { get; set; } = 1;

	/// <summary>
	/// Gets or sets the general version property for the message.
	/// </summary>
	/// <value>The current <see cref="Version"/> value.</value>
	public int Version { get; set; } = 1;
}
