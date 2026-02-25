// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSubscriptionManagerShould
{
	private readonly IEventStore _eventStore;
	private readonly IEventSerializer _eventSerializer;
	private readonly ILoggerFactory _loggerFactory;
	private readonly EventSubscriptionManager _sut;

	public EventSubscriptionManagerShould()
	{
		_eventStore = A.Fake<IEventStore>();
		_eventSerializer = A.Fake<IEventSerializer>();
		_loggerFactory = NullLoggerFactory.Instance;
		_sut = new EventSubscriptionManager(_eventStore, _eventSerializer, _loggerFactory);
	}

	[Fact]
	public void CreateSubscription_ReturnNewSubscription()
	{
		// Arrange
		var options = new EventSubscriptionOptions();

		// Act
		var subscription = _sut.CreateSubscription("order-sub", options);

		// Assert
		subscription.ShouldNotBeNull();
		subscription.ShouldBeOfType<EventStoreLiveSubscription>();
	}

	[Fact]
	public void CreateSubscription_ThrowOnDuplicateName()
	{
		// Arrange
		var options = new EventSubscriptionOptions();
		_sut.CreateSubscription("order-sub", options);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			_sut.CreateSubscription("order-sub", options));
	}

	[Fact]
	public void CreateSubscription_AllowMultipleDistinctNames()
	{
		// Arrange
		var options = new EventSubscriptionOptions();

		// Act
		var sub1 = _sut.CreateSubscription("sub-1", options);
		var sub2 = _sut.CreateSubscription("sub-2", options);

		// Assert
		sub1.ShouldNotBeNull();
		sub2.ShouldNotBeNull();
		sub1.ShouldNotBeSameAs(sub2);
	}

	[Fact]
	public void CreateSubscription_ThrowOnNullName()
	{
		var options = new EventSubscriptionOptions();
		Should.Throw<ArgumentException>(() => _sut.CreateSubscription(null!, options));
	}

	[Fact]
	public void CreateSubscription_ThrowOnEmptyName()
	{
		var options = new EventSubscriptionOptions();
		Should.Throw<ArgumentException>(() => _sut.CreateSubscription("", options));
	}

	[Fact]
	public void CreateSubscription_ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => _sut.CreateSubscription("sub", null!));
	}

	[Fact]
	public void GetSubscription_ReturnExistingSubscription()
	{
		// Arrange
		var options = new EventSubscriptionOptions();
		var created = _sut.CreateSubscription("order-sub", options);

		// Act
		var retrieved = _sut.GetSubscription("order-sub");

		// Assert
		retrieved.ShouldBeSameAs(created);
	}

	[Fact]
	public void GetSubscription_ReturnNull_WhenNotFound()
	{
		// Act
		var result = _sut.GetSubscription("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetSubscription_ThrowOnNullName()
	{
		Should.Throw<ArgumentException>(() => _sut.GetSubscription(null!));
	}

	[Fact]
	public void GetSubscription_ThrowOnEmptyName()
	{
		Should.Throw<ArgumentException>(() => _sut.GetSubscription(""));
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventSubscriptionManager(null!, _eventSerializer, _loggerFactory));
		Should.Throw<ArgumentNullException>(() =>
			new EventSubscriptionManager(_eventStore, null!, _loggerFactory));
		Should.Throw<ArgumentNullException>(() =>
			new EventSubscriptionManager(_eventStore, _eventSerializer, null!));
	}
}
