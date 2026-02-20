// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Supported serialization formats for message encoding.
/// This is the single canonical enum - all packages reference this definition.
/// </summary>
public enum SerializationFormat
{
	/// <summary>
	/// JSON serialization format (default).
	/// </summary>
	Json = 0,

	/// <summary>
	/// Protocol Buffers binary serialization format.
	/// </summary>
	Protobuf = 1,

	/// <summary>
	/// Apache Avro serialization format.
	/// </summary>
	Avro = 2,

	/// <summary>
	/// MessagePack binary serialization format.
	/// </summary>
	MessagePack = 3,

	/// <summary>
	/// XML serialization format.
	/// </summary>
	Xml = 4,
}
