// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Defines the encryption mode for field-level encryption operations during migration phases.
/// </summary>
/// <remarks>
/// <para>
/// These modes support a phased migration strategy from one encryption provider to another,
/// or from encrypted to unencrypted data.
/// </para>
/// <para>
/// The migration path typically follows: <see cref="EncryptAndDecrypt"/> → <see cref="EncryptNewDecryptAll"/>
/// → <see cref="DecryptOnlyWritePlaintext"/> → <see cref="Disabled"/>.
/// </para>
/// </remarks>
public enum EncryptionMode
{
	/// <summary>
	/// Normal operation: encrypt all writes, decrypt all reads.
	/// </summary>
	/// <remarks>
	/// This is the default mode for steady-state operation with field-level encryption enabled.
	/// </remarks>
	EncryptAndDecrypt = 0,

	/// <summary>
	/// Migration phase 1: encrypt new data with new provider, decrypt from any registered provider.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this mode when rotating to a new encryption key or provider. New data is encrypted
	/// with the primary provider, while reads can decrypt data encrypted by any legacy provider.
	/// </para>
	/// <para>
	/// This mode requires <see cref="IEncryptionProviderRegistry"/> to have legacy providers registered.
	/// </para>
	/// </remarks>
	EncryptNewDecryptAll = 1,

	/// <summary>
	/// Migration phase 2: decrypt all reads, write back as plaintext.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this mode when migrating away from encryption entirely. Reads decrypt encrypted data,
	/// but writes store data in plaintext. This allows gradual migration to unencrypted storage.
	/// </para>
	/// <para>
	/// <strong>Warning:</strong> This mode reduces security. Use only during planned migration
	/// with appropriate access controls in place.
	/// </para>
	/// </remarks>
	DecryptOnlyWritePlaintext = 2,

	/// <summary>
	/// Read-only migration mode: decrypt for reads, reject writes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this mode for read-only systems during migration verification or when the system
	/// should not modify encrypted data.
	/// </para>
	/// <para>
	/// Write operations in this mode will throw <see cref="InvalidOperationException"/>.
	/// </para>
	/// </remarks>
	DecryptOnlyReadOnly = 3,

	/// <summary>
	/// Encryption disabled: pass through all data without transformation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use this mode after migration is complete or for systems that do not require encryption.
	/// Data is read and written without any encryption or decryption.
	/// </para>
	/// <para>
	/// <strong>Note:</strong> Existing encrypted data will NOT be automatically decrypted.
	/// Use <see cref="DecryptOnlyWritePlaintext"/> first to migrate encrypted data.
	/// </para>
	/// </remarks>
	Disabled = 4
}
