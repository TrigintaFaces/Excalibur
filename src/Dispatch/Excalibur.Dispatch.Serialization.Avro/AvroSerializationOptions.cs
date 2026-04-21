// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization.Avro;

/// <summary>
/// Configuration options for Apache Avro serialization.
/// </summary>
public sealed class AvroSerializationOptions
{
	/// <summary>
	/// Gets or sets the buffer size used for Avro encoding operations.
	/// </summary>
	/// <value> Default is 4096 bytes. </value>
	public int BufferSize { get; set; } = 4096;
}
