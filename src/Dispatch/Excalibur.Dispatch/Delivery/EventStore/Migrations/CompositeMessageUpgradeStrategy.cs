// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Composite message upgrade strategy that delegates to multiple underlying strategies.
/// </summary>
/// <param name="strategies"> The collection of upgrade strategies to use. </param>
public sealed class CompositeMessageUpgradeStrategy(IEnumerable<IMessageUpgradeStrategy> strategies) : IMessageUpgradeStrategy
{
	/// <summary>
	/// Determines whether any of the underlying strategies can upgrade the specified message type and version.
	/// </summary>
	/// <param name="messageType"> The type of message to upgrade. </param>
	/// <param name="version"> The current version of the message. </param>
	/// <returns> True if any strategy can upgrade the message; otherwise, false. </returns>
	public bool CanUpgrade(Type messageType, string version) => strategies.Any(s => s.CanUpgrade(messageType, version));

	/// <summary>
	/// Upgrades a message using the first available strategy that can handle the upgrade.
	/// </summary>
	/// <param name="payload"> The serialized message payload. </param>
	/// <param name="messageType"> The type of message to upgrade. </param>
	/// <param name="fromVersion"> The current version of the message. </param>
	/// <param name="toVersion"> The target version to upgrade to. </param>
	/// <returns> The upgraded message object. </returns>
	/// <exception cref="NotSupportedException"> Thrown when no strategy can handle the upgrade. </exception>
	public object Upgrade(string payload, Type messageType, string fromVersion, string toVersion)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		var strategy = strategies.FirstOrDefault(s => s.CanUpgrade(messageType, fromVersion)) ??
					   throw new NotSupportedException($"No upgrade strategy found for {messageType.FullName} from version {fromVersion}");

		return strategy.Upgrade(payload, messageType, fromVersion, toVersion);
	}
}
