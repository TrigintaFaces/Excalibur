// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for upgrading messages from one version to another.
/// </summary>
public interface IMessageUpgradeStrategy
{
	/// <summary>
	/// Checks if the given version of the message type is upgradeable.
	/// </summary>
	bool CanUpgrade(Type messageType, string version);

	/// <summary>
	/// Upgrades the given payload (in string form) to the current version of the message type.
	/// </summary>
	object Upgrade(string payload, Type messageType, string fromVersion, string toVersion);
}
