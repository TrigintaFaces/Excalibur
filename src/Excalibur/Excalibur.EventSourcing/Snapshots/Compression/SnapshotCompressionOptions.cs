// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.IO.Compression;

namespace Excalibur.EventSourcing.Snapshots.Compression;

/// <summary>
/// Configuration options for snapshot compression.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>IOptions{T}</c> pattern from <c>Microsoft.Extensions.Options</c>.
/// </para>
/// </remarks>
public sealed class SnapshotCompressionOptions
{
	/// <summary>
	/// Gets or sets the compression algorithm to use.
	/// </summary>
	/// <value>The compression algorithm. Defaults to <see cref="SnapshotCompressionAlgorithm.Brotli"/>.</value>
	public SnapshotCompressionAlgorithm Algorithm { get; set; } = SnapshotCompressionAlgorithm.Brotli;

	/// <summary>
	/// Gets or sets the compression level.
	/// </summary>
	/// <value>The compression level. Defaults to <see cref="CompressionLevel.Fastest"/>.</value>
	public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Fastest;

	/// <summary>
	/// Gets or sets the minimum data size in bytes for compression to be applied.
	/// Data smaller than this threshold is stored uncompressed.
	/// </summary>
	/// <value>The minimum data size. Defaults to 256 bytes.</value>
	[Range(0, int.MaxValue)]
	public int MinimumSizeBytes { get; set; } = 256;
}

/// <summary>
/// Specifies the compression algorithm for snapshot data.
/// </summary>
public enum SnapshotCompressionAlgorithm
{
	/// <summary>
	/// Brotli compression (best ratio, good speed).
	/// </summary>
	Brotli = 0,

	/// <summary>
	/// GZip compression (widely compatible).
	/// </summary>
	GZip = 1,
}
