// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization.Protobuf;

/// <summary>
/// Defines the Protocol Buffers wire format options.
/// </summary>
public enum ProtobufWireFormat
{
	/// <summary>
	/// Binary wire format.
	/// </summary>
	Binary = 0,

	/// <summary>
	/// JSON wire format.
	/// </summary>
	Json = 1,
}
