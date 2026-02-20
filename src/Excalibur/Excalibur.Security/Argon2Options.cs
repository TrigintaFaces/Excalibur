// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Copyright (c) TrigintaFaces. All rights reserved.

namespace Excalibur.Security;

/// <summary>
/// Configuration options for Argon2id password hashing.
/// </summary>
/// <remarks>
/// Default values follow OWASP Password Storage Cheat Sheet recommendations (2024).
/// These defaults provide strong security while maintaining reasonable performance
/// on modern hardware.
/// </remarks>
public sealed class Argon2Options
{
	/// <summary>
	/// The configuration section name for options binding.
	/// </summary>
	public const string SectionName = "Argon2";

	/// <summary>
	/// Gets or sets the memory size in kilobytes.
	/// Default: 65536 KB (64 MB) per OWASP recommendations.
	/// </summary>
	/// <value>Memory size in KB. Must be at least 8192 KB (8 MB).</value>
	public int MemorySize { get; set; } = 65536;

	/// <summary>
	/// Gets or sets the number of iterations (time cost).
	/// Default: 4 iterations per OWASP recommendations.
	/// </summary>
	/// <value>Number of iterations. Must be at least 1.</value>
	public int Iterations { get; set; } = 4;

	/// <summary>
	/// Gets or sets the degree of parallelism.
	/// Default: 4 parallel lanes per OWASP recommendations.
	/// </summary>
	/// <value>Parallelism degree. Must be at least 1.</value>
	public int Parallelism { get; set; } = 4;

	/// <summary>
	/// Gets or sets the hash output length in bytes.
	/// Default: 32 bytes (256 bits).
	/// </summary>
	/// <value>Hash length in bytes. Must be at least 16 bytes.</value>
	public int HashLength { get; set; } = 32;

	/// <summary>
	/// Gets or sets the salt length in bytes.
	/// Default: 16 bytes (128 bits) per OWASP recommendations.
	/// </summary>
	/// <value>Salt length in bytes. Must be at least 16 bytes.</value>
	public int SaltLength { get; set; } = 16;

	/// <summary>
	/// Gets or sets the current version of the algorithm configuration.
	/// Used to detect when parameters have changed and rehashing is needed.
	/// </summary>
	/// <value>Configuration version number.</value>
	public int Version { get; set; } = 1;
}
