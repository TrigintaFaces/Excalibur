// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for message compression.
/// </summary>
public sealed class CompressionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether compression is enabled.
	/// </summary>
	/// <value> Default is false. </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the compression algorithm.
	/// </summary>
	/// <value> Default is Gzip. </value>
	public CompressionType CompressionType { get; set; } = CompressionType.Gzip;

	/// <summary>
	/// Gets or sets the compression level.
	/// </summary>
	/// <value> Default is 6 (balanced). </value>
	[Range(0, 9)]
	public int CompressionLevel { get; set; } = 6;

	/// <summary>
	/// Gets or sets the minimum message size threshold for compression.
	/// </summary>
	/// <value> Default is 1024 bytes. </value>
	[Range(0, int.MaxValue)]
	public int MinimumSizeThreshold { get; set; } = 1024;
}
