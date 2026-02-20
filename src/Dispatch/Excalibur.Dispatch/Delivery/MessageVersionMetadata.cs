// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Implementation of message version metadata for schema evolution and backward compatibility. This class provides version tracking
/// capabilities for message contracts, enabling safe schema evolution and maintaining compatibility across different versions of messaging components.
/// </summary>
public sealed class MessageVersionMetadata : IMessageVersionMetadata
{
	/// <summary>
	/// Gets or sets the schema version of the message structure. This version tracks changes to the message schema and enables backward
	/// compatibility handling.
	/// </summary>
	/// <value>The current <see cref="SchemaVersion"/> value.</value>
	public int SchemaVersion { get; set; }

	/// <summary>
	/// Gets or sets the serializer version used for message serialization. This version ensures compatibility between different serializer
	/// implementations and formats.
	/// </summary>
	/// <value>The current <see cref="SerializerVersion"/> value.</value>
	public int SerializerVersion { get; set; }

	/// <summary>
	/// Gets or sets the general message version for overall message evolution tracking. This version provides a unified versioning approach
	/// for message contract management.
	/// </summary>
	/// <value>The current <see cref="Version"/> value.</value>
	public int Version { get; set; }
}
