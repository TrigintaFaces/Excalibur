// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Upcasting;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Upcasting;

/// <summary>
/// Unit tests for <see cref="EventUpgrader{TFrom, TTo}"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EventUpgraderShould
{
	#region Test Events

	private sealed record TestEventV1(string OldField);
	private sealed record TestEventV2(string NewField, string AdditionalField);
	private sealed record DifferentEvent(string Value);

	#endregion Test Events

	#region Test Upgrader

	private sealed class TestV1ToV2Upgrader : EventUpgrader<TestEventV1, TestEventV2>
	{
		public override string EventType => "TestEvent";
		public override int FromVersion => 1;
		public override int ToVersion => 2;

		protected override TestEventV2 UpgradeEvent(TestEventV1 oldEvent)
			=> new(oldEvent.OldField, "DefaultValue");
	}

	#endregion Test Upgrader

	#region CanUpgrade Tests

	[Fact]
	public void CanUpgrade_ReturnTrue_WhenEventTypeAndVersionMatch()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act
		var result = upgrader.CanUpgrade("TestEvent", 1);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenEventTypeDoesNotMatch()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act
		var result = upgrader.CanUpgrade("OtherEvent", 1);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenVersionDoesNotMatch()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act
		var result = upgrader.CanUpgrade("TestEvent", 2);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenBothEventTypeAndVersionDoNotMatch()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act
		var result = upgrader.CanUpgrade("OtherEvent", 3);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenEventTypeIsNull()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act
		var result = upgrader.CanUpgrade(null!, 1);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_IsCaseSensitive()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act
		var result = upgrader.CanUpgrade("testevent", 1); // lowercase

		// Assert
		result.ShouldBeFalse();
	}

	#endregion CanUpgrade Tests

	#region Upgrade Tests

	[Fact]
	public void Upgrade_ShouldTransformEvent_WhenTypeMatches()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();
		var v1Event = new TestEventV1("OriginalValue");

		// Act
		var result = (TestEventV2)upgrader.Upgrade(v1Event);

		// Assert
		result.NewField.ShouldBe("OriginalValue");
		result.AdditionalField.ShouldBe("DefaultValue");
	}

	[Fact]
	public void Upgrade_ShouldThrowArgumentException_WhenEventTypeDoesNotMatch()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();
		var wrongEvent = new DifferentEvent("Value");

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => upgrader.Upgrade(wrongEvent));
		exception.Message.ShouldContain("Expected event of type");
		exception.Message.ShouldContain("TestEventV1");
	}

	[Fact]
	public void Upgrade_ShouldThrowArgumentException_WhenEventIsNull()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => upgrader.Upgrade(null!));
		exception.Message.ShouldContain("null");
	}

	[Fact]
	public void Upgrade_ShouldPreserveData_DuringTransformation()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();
		var v1Event = new TestEventV1("PreservedValue");

		// Act
		var result = (TestEventV2)upgrader.Upgrade(v1Event);

		// Assert
		result.NewField.ShouldBe("PreservedValue");
	}

	#endregion Upgrade Tests

	#region Properties Tests

	[Fact]
	public void EventType_ShouldReturnConfiguredValue()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act & Assert
		upgrader.EventType.ShouldBe("TestEvent");
	}

	[Fact]
	public void FromVersion_ShouldReturnConfiguredValue()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act & Assert
		upgrader.FromVersion.ShouldBe(1);
	}

	[Fact]
	public void ToVersion_ShouldReturnConfiguredValue()
	{
		// Arrange
		var upgrader = new TestV1ToV2Upgrader();

		// Act & Assert
		upgrader.ToVersion.ShouldBe(2);
	}

	#endregion Properties Tests
}
