// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Copyright (c) TrigintaFaces. All rights reserved.

namespace Excalibur.Security.Abstractions;

/// <summary>
/// Result of a password hashing operation containing all information needed
/// to verify the password later and detect when rehashing is needed.
/// </summary>
/// <remarks>
/// This record stores the hash along with all parameters used to create it,
/// enabling verification and detection of parameter changes that require rehashing.
/// </remarks>
public sealed record PasswordHashResult
{
	/// <summary>
	/// Gets the password hash (Base64 encoded).
	/// </summary>
	/// <value>The cryptographic hash of the password.</value>
	public required string Hash { get; init; }

	/// <summary>
	/// Gets the salt used for hashing (Base64 encoded).
	/// </summary>
	/// <value>The cryptographically random salt used during hashing.</value>
	public required string Salt { get; init; }

	/// <summary>
	/// Gets the algorithm identifier (e.g., "argon2id", "bcrypt").
	/// </summary>
	/// <value>The name of the hashing algorithm used.</value>
	public required string Algorithm { get; init; }

	/// <summary>
	/// Gets the algorithm version for future-proofing.
	/// </summary>
	/// <value>The version number of the algorithm parameters.</value>
	public required int Version { get; init; }

	/// <summary>
	/// Gets the algorithm-specific parameters (memory, iterations, parallelism).
	/// </summary>
	/// <value>A dictionary containing algorithm-specific configuration values.</value>
	public required IReadOnlyDictionary<string, object> Parameters { get; init; }
}
