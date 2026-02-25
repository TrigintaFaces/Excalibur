// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka compression types.
/// </summary>
public enum KafkaCompressionType
{
	/// <summary>
	/// No compression.
	/// </summary>
	None = 0,

	/// <summary>
	/// GZIP compression.
	/// </summary>
	Gzip = 1,

	/// <summary>
	/// Snappy compression (recommended for balance of speed and compression).
	/// </summary>
	Snappy = 2,

	/// <summary>
	/// LZ4 compression (fastest).
	/// </summary>
	Lz4 = 3,

	/// <summary>
	/// ZSTD compression (best compression ratio).
	/// </summary>
	Zstd = 4,
}
