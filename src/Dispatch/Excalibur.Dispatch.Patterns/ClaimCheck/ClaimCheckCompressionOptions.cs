// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Compression configuration options for the Claim Check pattern.
/// </summary>
public sealed class ClaimCheckCompressionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable compression for stored payloads.
	/// </summary>
	/// <value>A value indicating whether to enable compression for stored payloads.</value>
	public bool EnableCompression { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum size in bytes for compression to be applied.
	/// </summary>
	/// <value>The minimum size in bytes for compression to be applied.</value>
	public long CompressionThreshold { get; set; } = 1024; // 1KB default

	/// <summary>
	/// Gets or sets the minimum compression ratio (0.0 to 1.0) required to keep compressed data.
	/// </summary>
	/// <value>The minimum compression ratio required to keep compressed data.</value>
	public double MinCompressionRatio { get; set; } = 0.8;

	/// <summary>
	/// Gets or sets the compression level to use when compression is enabled.
	/// </summary>
	/// <value>The compression level to use when compression is enabled.</value>
	public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
}
