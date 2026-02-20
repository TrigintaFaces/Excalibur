// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.SessionManagement;

/// <summary>
/// Unit tests for <see cref="LockType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class LockTypeShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<LockType>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(LockType.Read);
		values.ShouldContain(LockType.Write);
		values.ShouldContain(LockType.UpgradeableRead);
	}

	[Fact]
	public void Read_HasExpectedValue()
	{
		// Assert
		((int)LockType.Read).ShouldBe(0);
	}

	[Fact]
	public void Write_HasExpectedValue()
	{
		// Assert
		((int)LockType.Write).ShouldBe(1);
	}

	[Fact]
	public void UpgradeableRead_HasExpectedValue()
	{
		// Assert
		((int)LockType.UpgradeableRead).ShouldBe(2);
	}

	[Fact]
	public void Read_IsDefaultValue()
	{
		// Arrange
		LockType defaultType = default;

		// Assert
		defaultType.ShouldBe(LockType.Read);
	}

	[Theory]
	[InlineData(LockType.Read)]
	[InlineData(LockType.Write)]
	[InlineData(LockType.UpgradeableRead)]
	public void BeDefinedForAllValues(LockType type)
	{
		// Assert
		Enum.IsDefined(type).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, LockType.Read)]
	[InlineData(1, LockType.Write)]
	[InlineData(2, LockType.UpgradeableRead)]
	public void CastFromInt_ReturnsCorrectValue(int value, LockType expected)
	{
		// Act
		var type = (LockType)value;

		// Assert
		type.ShouldBe(expected);
	}

	[Fact]
	public void HaveLockTypesOrderedByExclusivity()
	{
		// Assert - Read is least exclusive, UpgradeableRead allows upgrade to Write
		(LockType.Read < LockType.Write).ShouldBeTrue();
		(LockType.Write < LockType.UpgradeableRead).ShouldBeTrue();
	}
}
