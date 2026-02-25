// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Defines the supported compression algorithms.
/// </summary>
public enum CompressionType
{
	/// <summary>
	/// No compression.
	/// </summary>
	None = 0,

	/// <summary>
	/// Gzip compression.
	/// </summary>
	Gzip = 1,

	/// <summary>
	/// Deflate compression.
	/// </summary>
	Deflate = 2,

	/// <summary>
	/// LZ4 compression (fast).
	/// </summary>
	Lz4 = 3,

	/// <summary>
	/// Brotli compression (high ratio).
	/// </summary>
	Brotli = 4,
}
