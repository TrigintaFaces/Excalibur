// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="EntityStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class EntityStatusShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<EntityStatus>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(EntityStatus.Active);
		values.ShouldContain(EntityStatus.Disabled);
		values.ShouldContain(EntityStatus.ReceiveDisabled);
		values.ShouldContain(EntityStatus.SendDisabled);
	}

	[Fact]
	public void Active_HasExpectedValue()
	{
		// Assert
		((int)EntityStatus.Active).ShouldBe(0);
	}

	[Fact]
	public void Disabled_HasExpectedValue()
	{
		// Assert
		((int)EntityStatus.Disabled).ShouldBe(1);
	}

	[Fact]
	public void ReceiveDisabled_HasExpectedValue()
	{
		// Assert
		((int)EntityStatus.ReceiveDisabled).ShouldBe(2);
	}

	[Fact]
	public void SendDisabled_HasExpectedValue()
	{
		// Assert
		((int)EntityStatus.SendDisabled).ShouldBe(3);
	}

	[Fact]
	public void Active_IsDefaultValue()
	{
		// Arrange
		EntityStatus defaultStatus = default;

		// Assert
		defaultStatus.ShouldBe(EntityStatus.Active);
	}

	[Theory]
	[InlineData(EntityStatus.Active)]
	[InlineData(EntityStatus.Disabled)]
	[InlineData(EntityStatus.ReceiveDisabled)]
	[InlineData(EntityStatus.SendDisabled)]
	public void BeDefinedForAllValues(EntityStatus status)
	{
		// Assert
		Enum.IsDefined(status).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, EntityStatus.Active)]
	[InlineData(1, EntityStatus.Disabled)]
	[InlineData(2, EntityStatus.ReceiveDisabled)]
	[InlineData(3, EntityStatus.SendDisabled)]
	public void CastFromInt_ReturnsCorrectValue(int value, EntityStatus expected)
	{
		// Act
		var status = (EntityStatus)value;

		// Assert
		status.ShouldBe(expected);
	}

	[Fact]
	public void Active_AllowsBothSendAndReceive()
	{
		// Assert - Active is the fully operational state
		var status = EntityStatus.Active;
		status.ShouldNotBe(EntityStatus.Disabled);
		status.ShouldNotBe(EntityStatus.ReceiveDisabled);
		status.ShouldNotBe(EntityStatus.SendDisabled);
	}

	[Fact]
	public void Disabled_BlocksBothSendAndReceive()
	{
		// Assert - Disabled blocks both operations
		var status = EntityStatus.Disabled;
		status.ShouldNotBe(EntityStatus.Active);
	}
}
