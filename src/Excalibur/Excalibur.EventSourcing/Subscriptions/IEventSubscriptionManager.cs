// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Manages named event subscriptions, providing creation and lookup capabilities.
/// </summary>
/// <remarks>
/// <para>
/// The subscription manager maintains a registry of named subscriptions. Each subscription
/// is uniquely identified by its name and configured via <see cref="EventSubscriptionOptions"/>.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// var subscription = manager.CreateSubscription("order-projection",
///     new EventSubscriptionOptions { StartPosition = SubscriptionStartPosition.Beginning });
/// await subscription.SubscribeAsync("Order-123", HandleEvents, ct);
/// </code>
/// </para>
/// </remarks>
public interface IEventSubscriptionManager
{
	/// <summary>
	/// Creates a new named event subscription with the specified options.
	/// </summary>
	/// <param name="name">The unique subscription name.</param>
	/// <param name="options">The subscription configuration options.</param>
	/// <returns>The created event subscription.</returns>
	IEventSubscription CreateSubscription(string name, EventSubscriptionOptions options);

	/// <summary>
	/// Gets an existing subscription by name.
	/// </summary>
	/// <param name="name">The subscription name.</param>
	/// <returns>The subscription if found; otherwise, <see langword="null"/>.</returns>
	IEventSubscription? GetSubscription(string name);
}
