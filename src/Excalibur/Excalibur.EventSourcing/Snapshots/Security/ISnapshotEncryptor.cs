// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Snapshots.Security;

/// <summary>
/// Defines the contract for encrypting and decrypting snapshot data.
/// </summary>
/// <remarks>
/// <para>
/// Implementations may use <c>IDataProtector</c>, AES, or any other encryption mechanism.
/// The interface is intentionally minimal (2 methods) following the Microsoft design pattern
/// for focused abstractions.
/// </para>
/// </remarks>
public interface ISnapshotEncryptor
{
	/// <summary>
	/// Encrypts the specified snapshot data.
	/// </summary>
	/// <param name="data">The plaintext snapshot data.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The encrypted snapshot data.</returns>
	ValueTask<byte[]> EncryptAsync(byte[] data, CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts the specified snapshot data.
	/// </summary>
	/// <param name="data">The encrypted snapshot data.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The decrypted snapshot data.</returns>
	ValueTask<byte[]> DecryptAsync(byte[] data, CancellationToken cancellationToken);
}
