// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

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
	Suspended = 4
}
