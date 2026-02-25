// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Upcasting;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Upcasting;

/// <summary>
/// Unit tests for <see cref="EventVersionManager"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EventVersionManagerShould
{
	private readonly EventVersionManager _manager;

	public EventVersionManagerShould()
	{
		_manager = new EventVersionManager(NullLogger<EventVersionManager>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new EventVersionManager(null!));
	}

	#endregion Constructor Tests

	#region RegisterUpgrader Tests

	[Fact]
	public void RegisterUpgrader_ShouldAddUpgraderToRegistry()
	{
		// Arrange
		var upgrader = CreateMockUpgrader("TestEvent", 1, 2);

		// Act
		_manager.RegisterUpgrader(upgrader);

		// Assert
		var upgraders = _manager.GetUpgradersForEventType("TestEvent");
		upgraders.ShouldContain(upgrader);
	}

	[Fact]
	public void RegisterUpgrader_ShouldThrowArgumentNullException_WhenUpgraderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _manager.RegisterUpgrader(null!));
	}

	[Fact]
	public void RegisterUpgrader_ShouldThrowInvalidOperationException_WhenDuplicateUpgraderRegistered()
	{
		// Arrange
		var upgrader1 = CreateMockUpgrader("TestEvent", 1, 2);
		var upgrader2 = CreateMockUpgrader("TestEvent", 1, 2);

		_manager.RegisterUpgrader(upgrader1);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _manager.RegisterUpgrader(upgrader2));
	}

	[Fact]
	public void RegisterUpgrader_ShouldAllowMultipleUpgradersForSameEventType()
	{
		// Arrange
		var upgrader1To2 = CreateMockUpgrader("TestEvent", 1, 2);
		var upgrader2To3 = CreateMockUpgrader("TestEvent", 2, 3);

		// Act
		_manager.RegisterUpgrader(upgrader1To2);
		_manager.RegisterUpgrader(upgrader2To3);

		// Assert
		var upgraders = _manager.GetUpgradersForEventType("TestEvent").ToList();
		upgraders.Count.ShouldBe(2);
	}

	[Fact]
	public void RegisterUpgrader_ShouldAllowSameVersionRangeForDifferentEventTypes()
	{
		// Arrange
		var upgrader1 = CreateMockUpgrader("EventA", 1, 2);
		var upgrader2 = CreateMockUpgrader("EventB", 1, 2);

		// Act & Assert - should not throw
		_manager.RegisterUpgrader(upgrader1);
		_manager.RegisterUpgrader(upgrader2);
	}

	#endregion RegisterUpgrader Tests

	#region UpgradeEvent Tests

	[Fact]
	public void UpgradeEvent_ShouldReturnSameEvent_WhenVersionsAreEqual()
	{
		// Arrange
		var eventData = new { Value = "test" };

		// Act
		var result = _manager.UpgradeEvent("TestEvent", eventData, 1, 1);

		// Assert
		result.ShouldBeSameAs(eventData);
	}

	[Fact]
	public void UpgradeEvent_ShouldThrowArgumentException_WhenEventTypeIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_manager.UpgradeEvent(null!, new object(), 1, 2));
	}

	[Fact]
	public void UpgradeEvent_ShouldThrowArgumentException_WhenEventTypeIsEmpty()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_manager.UpgradeEvent(string.Empty, new object(), 1, 2));
	}

	[Fact]
	public void UpgradeEvent_ShouldThrowArgumentNullException_WhenEventDataIsNull()
	{
		// Arrange
		var upgrader = CreateMockUpgrader("TestEvent", 1, 2);
		_manager.RegisterUpgrader(upgrader);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_manager.UpgradeEvent("TestEvent", null!, 1, 2));
	}

	[Fact]
	public void UpgradeEvent_ShouldThrowInvalidOperationException_WhenNoUpgradersRegistered()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			_manager.UpgradeEvent("UnknownEvent", new object(), 1, 2));
	}

	[Fact]
	public void UpgradeEvent_ShouldThrowInvalidOperationException_WhenNoUpgradePathFound()
	{
		// Arrange - Register upgrader from 1 to 2, but ask for 1 to 5
		var upgrader = CreateMockUpgrader("TestEvent", 1, 2);
		_manager.RegisterUpgrader(upgrader);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			_manager.UpgradeEvent("TestEvent", new object(), 1, 5));
	}

	[Fact]
	public void UpgradeEvent_ShouldUpgradeDirectly_WhenDirectPathExists()
	{
		// Arrange
		var originalEvent = new TestEventV1("original");
		var upgradedEvent = new TestEventV2("upgraded");

		var upgrader = A.Fake<IEventUpgrader>();
		_ = A.CallTo(() => upgrader.EventType).Returns("TestEvent");
		_ = A.CallTo(() => upgrader.FromVersion).Returns(1);
		_ = A.CallTo(() => upgrader.ToVersion).Returns(2);
		_ = A.CallTo(() => upgrader.Upgrade(originalEvent)).Returns(upgradedEvent);

		_manager.RegisterUpgrader(upgrader);

		// Act
		var result = _manager.UpgradeEvent("TestEvent", originalEvent, 1, 2);

		// Assert
		result.ShouldBe(upgradedEvent);
		_ = A.CallTo(() => upgrader.Upgrade(originalEvent)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UpgradeEvent_ShouldChainUpgraders_WhenMultipleStepsRequired()
	{
		// Arrange
		var v1Event = new TestEventV1("v1");
		var v2Event = new TestEventV2("v2");
		var v3Event = new TestEventV3("v3");

		var upgrader1To2 = A.Fake<IEventUpgrader>();
		_ = A.CallTo(() => upgrader1To2.EventType).Returns("TestEvent");
		_ = A.CallTo(() => upgrader1To2.FromVersion).Returns(1);
		_ = A.CallTo(() => upgrader1To2.ToVersion).Returns(2);
		_ = A.CallTo(() => upgrader1To2.Upgrade(v1Event)).Returns(v2Event);

		var upgrader2To3 = A.Fake<IEventUpgrader>();
		_ = A.CallTo(() => upgrader2To3.EventType).Returns("TestEvent");
		_ = A.CallTo(() => upgrader2To3.FromVersion).Returns(2);
		_ = A.CallTo(() => upgrader2To3.ToVersion).Returns(3);
		_ = A.CallTo(() => upgrader2To3.Upgrade(v2Event)).Returns(v3Event);

		_manager.RegisterUpgrader(upgrader1To2);
		_manager.RegisterUpgrader(upgrader2To3);

		// Act
		var result = _manager.UpgradeEvent("TestEvent", v1Event, 1, 3);

		// Assert
		result.ShouldBe(v3Event);
		_ = A.CallTo(() => upgrader1To2.Upgrade(v1Event)).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => upgrader2To3.Upgrade(v2Event)).MustHaveHappenedOnceExactly();
	}

	#endregion UpgradeEvent Tests

	#region GetRegisteredEventTypes Tests

	[Fact]
	public void GetRegisteredEventTypes_ShouldReturnEmpty_WhenNoUpgradersRegistered()
	{
		// Act
		var eventTypes = _manager.GetRegisteredEventTypes();

		// Assert
		eventTypes.ShouldBeEmpty();
	}

	[Fact]
	public void GetRegisteredEventTypes_ShouldReturnAllRegisteredEventTypes()
	{
		// Arrange
		_manager.RegisterUpgrader(CreateMockUpgrader("EventA", 1, 2));
		_manager.RegisterUpgrader(CreateMockUpgrader("EventB", 1, 2));

		// Act
		var eventTypes = _manager.GetRegisteredEventTypes().ToList();

		// Assert
		eventTypes.Count.ShouldBe(2);
		eventTypes.ShouldContain("EventA");
		eventTypes.ShouldContain("EventB");
	}

	#endregion GetRegisteredEventTypes Tests

	#region GetUpgradersForEventType Tests

	[Fact]
	public void GetUpgradersForEventType_ShouldReturnEmpty_WhenEventTypeNotRegistered()
	{
		// Act
		var upgraders = _manager.GetUpgradersForEventType("UnknownEvent");

		// Assert
		upgraders.ShouldBeEmpty();
	}

	[Fact]
	public void GetUpgradersForEventType_ShouldReturnAllUpgraders_ForGivenEventType()
	{
		// Arrange
		var upgrader1To2 = CreateMockUpgrader("TestEvent", 1, 2);
		var upgrader2To3 = CreateMockUpgrader("TestEvent", 2, 3);

		_manager.RegisterUpgrader(upgrader1To2);
		_manager.RegisterUpgrader(upgrader2To3);

		// Act
		var upgraders = _manager.GetUpgradersForEventType("TestEvent").ToList();

		// Assert
		upgraders.Count.ShouldBe(2);
		upgraders.ShouldContain(upgrader1To2);
		upgraders.ShouldContain(upgrader2To3);
	}

	#endregion GetUpgradersForEventType Tests

	#region Helper Methods

	private static IEventUpgrader CreateMockUpgrader(string eventType, int fromVersion, int toVersion)
	{
		var upgrader = A.Fake<IEventUpgrader>();
		_ = A.CallTo(() => upgrader.EventType).Returns(eventType);
		_ = A.CallTo(() => upgrader.FromVersion).Returns(fromVersion);
		_ = A.CallTo(() => upgrader.ToVersion).Returns(toVersion);
		return upgrader;
	}

	#endregion Helper Methods

	#region Test Events

	private sealed record TestEventV1(string Value);
	private sealed record TestEventV2(string Value);
	private sealed record TestEventV3(string Value);

	#endregion Test Events
}
