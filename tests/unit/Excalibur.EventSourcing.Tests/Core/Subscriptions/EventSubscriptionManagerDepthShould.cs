// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

/// <summary>
/// Depth coverage tests for <see cref="EventSubscriptionManager"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSubscriptionManagerDepthShould
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenEventStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventSubscriptionManager(null!, A.Fake<IEventSerializer>(), NullLoggerFactory.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenSerializerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventSubscriptionManager(A.Fake<IEventStore>(), null!, NullLoggerFactory.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerFactoryIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventSubscriptionManager(A.Fake<IEventStore>(), A.Fake<IEventSerializer>(), null!));
	}

	[Fact]
	public void CreateSubscription_ReturnsSubscription()
	{
		// Arrange
		var manager = CreateManager();
		var options = new EventSubscriptionOptions();

		// Act
		var subscription = manager.CreateSubscription("test-sub", options);

		// Assert
		subscription.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSubscription_ThrowsArgumentException_WhenNameIsNull()
	{
		var manager = CreateManager();
		Should.Throw<ArgumentException>(() =>
			manager.CreateSubscription(null!, new EventSubscriptionOptions()));
	}

	[Fact]
	public void CreateSubscription_ThrowsArgumentException_WhenNameIsEmpty()
	{
		var manager = CreateManager();
		Should.Throw<ArgumentException>(() =>
			manager.CreateSubscription("", new EventSubscriptionOptions()));
	}

	[Fact]
	public void CreateSubscription_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		var manager = CreateManager();
		Should.Throw<ArgumentNullException>(() =>
			manager.CreateSubscription("test-sub", null!));
	}

	[Fact]
	public void CreateSubscription_ThrowsInvalidOperationException_WhenDuplicate()
	{
		// Arrange
		var manager = CreateManager();
		var options = new EventSubscriptionOptions();
		manager.CreateSubscription("my-sub", options);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			manager.CreateSubscription("my-sub", options));
	}

	[Fact]
	public void GetSubscription_ReturnsNull_WhenNotFound()
	{
		var manager = CreateManager();
		manager.GetSubscription("unknown").ShouldBeNull();
	}

	[Fact]
	public void GetSubscription_ReturnsSubscription_WhenExists()
	{
		// Arrange
		var manager = CreateManager();
		var options = new EventSubscriptionOptions();
		var created = manager.CreateSubscription("my-sub", options);

		// Act
		var retrieved = manager.GetSubscription("my-sub");

		// Assert
		retrieved.ShouldBeSameAs(created);
	}

	[Fact]
	public void GetSubscription_ThrowsArgumentException_WhenNameIsNull()
	{
		var manager = CreateManager();
		Should.Throw<ArgumentException>(() => manager.GetSubscription(null!));
	}

	[Fact]
	public void GetSubscription_ThrowsArgumentException_WhenNameIsEmpty()
	{
		var manager = CreateManager();
		Should.Throw<ArgumentException>(() => manager.GetSubscription(""));
	}

	private static EventSubscriptionManager CreateManager()
	{
		return new EventSubscriptionManager(
			A.Fake<IEventStore>(),
			A.Fake<IEventSerializer>(),
			NullLoggerFactory.Instance);
	}
}
