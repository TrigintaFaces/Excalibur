// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Options;

/// <summary>
/// Serialization options for controlling compression, size limits, and other features.
/// </summary>
public sealed class SerializationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to use compression.
	/// </summary>
	/// <value> The current <see cref="UseCompression" /> value. </value>
	public bool UseCompression { get; set; }

	/// <summary>
	/// Gets or sets the compression algorithm to use.
	/// </summary>
	/// <value> The current <see cref="CompressionAlgorithm" /> value. </value>
	public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.None;

	/// <summary>
	/// Gets or sets a value indicating whether to include type information in the serialized data.
	/// </summary>
	/// <value> The current <see cref="IncludeTypeInfo" /> value. </value>
	public bool IncludeTypeInfo { get; set; }

	/// <summary>
	/// Gets or sets the maximum allowed message size in bytes.
	/// </summary>
	/// <value> The current <see cref="MaxMessageSize" /> value. </value>
	public int MaxMessageSize { get; set; } = 256 * 1024; // 256 KB default

}
