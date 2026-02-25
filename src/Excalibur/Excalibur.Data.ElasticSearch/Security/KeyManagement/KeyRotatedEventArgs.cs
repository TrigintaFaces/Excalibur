// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides data for the KeyRotated event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KeyRotatedEventArgs" /> class.
/// </remarks>
/// <param name="keyName"> The name of the rotated key. </param>
/// <param name="newKeyVersion"> The version of the new key. </param>
/// <param name="rotatedAt"> The timestamp when the rotation occurred. </param>
/// <param name="nextRotationDue"> When the next rotation is due. </param>
public sealed class KeyRotatedEventArgs(string keyName, string newKeyVersion, DateTimeOffset rotatedAt, DateTimeOffset? nextRotationDue)
	: EventArgs
{
	/// <summary>
	/// Gets the name of the rotated key.
	/// </summary>
	/// <value> The unique identifier for the key that was rotated. </value>
	public string KeyName { get; } = keyName ?? throw new ArgumentNullException(nameof(keyName));

	/// <summary>
	/// Gets the version of the new key after rotation.
	/// </summary>
	/// <value> The version identifier for the new key. </value>
	public string NewKeyVersion { get; } = newKeyVersion ?? throw new ArgumentNullException(nameof(newKeyVersion));

	/// <summary>
	/// Gets the timestamp when the rotation occurred.
	/// </summary>
	/// <value> The UTC timestamp of the key rotation. </value>
	public DateTimeOffset RotatedAt { get; } = rotatedAt;

	/// <summary>
	/// Gets when the next rotation is due.
	/// </summary>
	/// <value> The UTC timestamp for the next scheduled rotation. </value>
	public DateTimeOffset? NextRotationDue { get; } = nextRotationDue;
}
