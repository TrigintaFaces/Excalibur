// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization.Protobuf;

/// <summary>
/// Configuration options for Protocol Buffers serialization.
/// </summary>
public sealed class ProtobufSerializationOptions
{
	/// <summary>
	/// Gets or sets the wire format to use.
	/// </summary>
	/// <value> Default is Binary. </value>
	public ProtobufWireFormat WireFormat { get; set; } = ProtobufWireFormat.Binary;
}
