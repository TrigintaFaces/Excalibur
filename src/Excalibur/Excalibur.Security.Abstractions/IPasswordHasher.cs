// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Copyright (c) TrigintaFaces. All rights reserved.

namespace Excalibur.Security.Abstractions;

/// <summary>
/// Provides password hashing and verification using secure algorithms.
/// </summary>
/// <remarks>
/// Implementations should use cryptographically secure algorithms such as Argon2id,
/// bcrypt, or scrypt. The default implementation uses Argon2id with OWASP-recommended
/// parameters.
/// </remarks>
public interface IPasswordHasher
{
	/// <summary>
	/// Hashes a password using a cryptographically secure algorithm.
	/// </summary>
	/// <param name="password">The plaintext password to hash.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A structured result containing the hash, salt, and algorithm parameters.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="password"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="password"/> is empty or whitespace.</exception>
	Task<PasswordHashResult> HashPasswordAsync(
		string password,
		CancellationToken cancellationToken);

	/// <summary>
	/// Verifies a password against a stored hash.
	/// </summary>
	/// <param name="password">The plaintext password to verify.</param>
	/// <param name="storedHash">The previously stored hash result.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Verification result indicating success, failure, or need to rehash.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="password"/> or <paramref name="storedHash"/> is null.</exception>
	Task<PasswordVerificationResult> VerifyPasswordAsync(
		string password,
		PasswordHashResult storedHash,
		CancellationToken cancellationToken);
}
