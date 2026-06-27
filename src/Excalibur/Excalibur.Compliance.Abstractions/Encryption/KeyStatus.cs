// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Represents the lifecycle status of an encryption key.
/// </summary>
/// <remarks>
/// Keys follow a lifecycle: Active -&gt; Decrypt-Only -&gt; Destroyed. Regular key rotation is required for compliance.
/// </remarks>
public enum KeyStatus
{
	/// <summary>
	/// Key is active and can be used for both encryption and decryption.
	/// </summary>
	Active = 0,

	/// <summary>
	/// Key can only be used for decryption (rotated out). New encryptions must use a different key.
	/// </summary>
	DecryptOnly = 1,

	/// <summary>
	/// Key is scheduled for destruction. No operations allowed; awaiting deletion.
	/// </summary>
	PendingDestruction = 2,

	/// <summary>
	/// Key has been destroyed and cannot be recovered.
	/// </summary>
	Destroyed = 3,

	/// <summary>
	/// Key is suspended due to security concerns. Requires administrative action to reactivate or destroy.
	/// </summary>
	/// <remarks>
	/// A suspended key is unusable for <b>both</b> encryption and decryption (full quarantine) — a stronger
	/// posture than <see cref="DecryptOnly"/>, which blocks only new encryption while keeping existing
	/// ciphertext decryptable. Use <see cref="DecryptOnly"/> when the intent is "stop new encryption but
	/// keep reading existing data"; use <see cref="Suspended"/> when a key is compromised and must be
	/// quarantined entirely until an administrator reactivates or destroys it.
	/// </remarks>
	Suspended = 4
}
