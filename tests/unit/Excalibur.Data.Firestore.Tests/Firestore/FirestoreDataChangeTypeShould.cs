// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreDataChangeType"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify enum values.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "CDC")]
public sealed class FirestoreDataChangeTypeShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveAddedValue()
	{
		// Assert
		Enum.IsDefined(FirestoreDataChangeType.Added).ShouldBeTrue();
	}

	[Fact]
	public void HaveModifiedValue()
	{
		// Assert
		Enum.IsDefined(FirestoreDataChangeType.Modified).ShouldBeTrue();
	}

	[Fact]
	public void HaveRemovedValue()
	{
		// Assert
		Enum.IsDefined(FirestoreDataChangeType.Removed).ShouldBeTrue();
	}

	[Fact]
	public void HaveExactlyThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<FirestoreDataChangeType>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void HaveAddedAsZero()
	{
		// Assert
		((int)FirestoreDataChangeType.Added).ShouldBe(0);
	}

	[Fact]
	public void HaveModifiedAsOne()
	{
		// Assert
		((int)FirestoreDataChangeType.Modified).ShouldBe(1);
	}

	[Fact]
	public void HaveRemovedAsTwo()
	{
		// Assert
		((int)FirestoreDataChangeType.Removed).ShouldBe(2);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Added", FirestoreDataChangeType.Added)]
	[InlineData("Modified", FirestoreDataChangeType.Modified)]
	[InlineData("Removed", FirestoreDataChangeType.Removed)]
	public void ParseFromString(string input, FirestoreDataChangeType expected)
	{
		// Act
		var result = Enum.Parse<FirestoreDataChangeType>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ThrowFormatException_ForInvalidString()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => Enum.Parse<FirestoreDataChangeType>("Invalid"));
	}

	#endregion

	#region ToString Tests

	[Theory]
	[InlineData(FirestoreDataChangeType.Added, "Added")]
	[InlineData(FirestoreDataChangeType.Modified, "Modified")]
	[InlineData(FirestoreDataChangeType.Removed, "Removed")]
	public void ReturnCorrectStringRepresentation(FirestoreDataChangeType value, string expected)
	{
		// Act
		var result = value.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(FirestoreDataChangeType).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void BeAnEnum()
	{
		// Assert
		typeof(FirestoreDataChangeType).IsEnum.ShouldBeTrue();
	}

	#endregion
}
