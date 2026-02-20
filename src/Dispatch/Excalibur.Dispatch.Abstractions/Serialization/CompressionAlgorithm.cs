// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Supported compression algorithms for message serialization.
/// This is the single canonical enum — all packages reference this definition.
/// </summary>
public enum CompressionAlgorithm
{
	/// <summary>
	/// No compression.
	/// </summary>
	None = 0,

	/// <summary>
	/// Gzip compression - good balance of compression ratio and speed.
	/// </summary>
	Gzip = 1,

	/// <summary>
	/// Brotli compression - better compression ratio but slower than Gzip.
	/// </summary>
	Brotli = 2,

	/// <summary>
	/// LZ4 compression - very fast compression with moderate ratio.
	/// </summary>
	Lz4 = 3,

	/// <summary>
	/// Zstandard compression - excellent compression ratio and speed.
	/// </summary>
	Zstd = 4,

	/// <summary>
	/// Deflate compression - raw deflate algorithm without Gzip headers.
	/// </summary>
	Deflate = 5,

	/// <summary>
	/// Snappy compression - very fast with reasonable compression ratio.
	/// </summary>
	Snappy = 6,
}
