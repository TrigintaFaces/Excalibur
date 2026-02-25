// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Controls when lazy migration encrypts plaintext data during normal operations.
/// </summary>
/// <remarks>
/// <para>
/// Lazy migration provides opportunistic encryption of plaintext data
/// encountered during normal read/write operations. This enables gradual migration
/// without requiring dedicated batch processing.
/// </para>
/// <para>
/// Use in conjunction with <c>EncryptionOptions.LazyMigrationEnabled</c>.
/// </para>
/// </remarks>
public enum LazyMigrationMode
{
	/// <summary>
	/// Lazy migration is disabled. Plaintext data is not encrypted opportunistically.
	/// </summary>
	Disabled = 0,

	/// <summary>
	/// Encrypt plaintext data when reading (opportunistic read-through encryption).
	/// </summary>
	/// <remarks>
	/// <para>
	/// When plaintext data is read, it is encrypted and written back to the store.
	/// This provides gradual migration as data is accessed.
	/// </para>
	/// <para>
	/// <strong>Note:</strong> This mode incurs additional write operations on reads.
	/// </para>
	/// </remarks>
	OnRead = 1,

	/// <summary>
	/// Encrypt plaintext data only on write operations.
	/// </summary>
	/// <remarks>
	/// When data is written, any plaintext fields are encrypted before storage.
	/// Reads do not trigger encryption - plaintext is returned as-is.
	/// </remarks>
	OnWrite = 2,

	/// <summary>
	/// Encrypt plaintext data on both read and write operations.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Provides the fastest migration by encrypting plaintext data whenever it is accessed.
	/// </para>
	/// <para>
	/// This is the recommended mode for active migration phases when
	/// <c>EncryptionOptions.LazyMigrationEnabled</c> is <c>true</c>.
	/// </para>
	/// </remarks>
	Both = 3
}
