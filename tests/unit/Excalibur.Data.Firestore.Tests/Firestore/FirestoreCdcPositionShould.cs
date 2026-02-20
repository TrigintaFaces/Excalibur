// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreCdcPosition"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify CDC position creation, serialization, and comparison.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "CDC")]
public sealed class FirestoreCdcPositionShould
{
	private const string TestCollectionPath = "test-collection";
	private const string TestDocumentId = "doc-123";

	#region Beginning Factory Method Tests

	[Fact]
	public void Beginning_CreatesPositionWithCollectionPath()
	{
		// Act
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Assert
		position.CollectionPath.ShouldBe(TestCollectionPath);
	}

	[Fact]
	public void Beginning_HasNullUpdateTime()
	{
		// Act
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Assert
		position.UpdateTime.ShouldBeNull();
	}

	[Fact]
	public void Beginning_HasNullLastDocumentId()
	{
		// Act
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Assert
		position.LastDocumentId.ShouldBeNull();
	}

	[Fact]
	public void Beginning_HasNullTimestamp()
	{
		// Act
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Assert
		position.Timestamp.ShouldBeNull();
	}

	[Fact]
	public void Beginning_IsNotValid()
	{
		// Act
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Assert
		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Beginning_IsBeginning()
	{
		// Act
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Assert
		position.IsBeginning.ShouldBeTrue();
	}

	#endregion

	#region Now Factory Method Tests

	[Fact]
	public void Now_CreatesPositionWithCollectionPath()
	{
		// Act
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Assert
		position.CollectionPath.ShouldBe(TestCollectionPath);
	}

	[Fact]
	public void Now_HasUpdateTime()
	{
		// Act
		var before = DateTimeOffset.UtcNow;
		var position = FirestoreCdcPosition.Now(TestCollectionPath);
		var after = DateTimeOffset.UtcNow;

		// Assert
		position.UpdateTime.ShouldNotBeNull();
		position.UpdateTime.Value.ShouldBeGreaterThanOrEqualTo(before);
		position.UpdateTime.Value.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Now_HasNullLastDocumentId()
	{
		// Act
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Assert
		position.LastDocumentId.ShouldBeNull();
	}

	[Fact]
	public void Now_HasTimestamp()
	{
		// Act
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Assert
		position.Timestamp.ShouldNotBeNull();
	}

	[Fact]
	public void Now_IsValid()
	{
		// Act
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Assert
		position.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Now_IsNotBeginning()
	{
		// Act
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Assert
		position.IsBeginning.ShouldBeFalse();
	}

	#endregion

	#region FromUpdateTime Factory Method Tests

	[Fact]
	public void FromUpdateTime_CreatesPositionWithCorrectValues()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;

		// Act
		var position = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);

		// Assert
		position.CollectionPath.ShouldBe(TestCollectionPath);
		position.UpdateTime.ShouldBe(updateTime);
		position.LastDocumentId.ShouldBe(TestDocumentId);
		position.Timestamp.ShouldNotBeNull();
	}

	[Fact]
	public void FromUpdateTime_WithNullDocumentId_CreatesValidPosition()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;

		// Act
		var position = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, null);

		// Assert
		position.LastDocumentId.ShouldBeNull();
		position.IsValid.ShouldBeTrue();
	}

	#endregion

	#region WithDocument Tests

	[Fact]
	public void WithDocument_CreatesNewPositionWithUpdatedValues()
	{
		// Arrange
		var original = FirestoreCdcPosition.Beginning(TestCollectionPath);
		var updateTime = DateTimeOffset.UtcNow;

		// Act
		var updated = original.WithDocument(updateTime, TestDocumentId);

		// Assert
		updated.UpdateTime.ShouldBe(updateTime);
		updated.LastDocumentId.ShouldBe(TestDocumentId);
		updated.CollectionPath.ShouldBe(TestCollectionPath);
	}

	[Fact]
	public void WithDocument_DoesNotModifyOriginal()
	{
		// Arrange
		var original = FirestoreCdcPosition.Beginning(TestCollectionPath);
		var updateTime = DateTimeOffset.UtcNow;

		// Act
		_ = original.WithDocument(updateTime, TestDocumentId);

		// Assert
		original.UpdateTime.ShouldBeNull();
		original.LastDocumentId.ShouldBeNull();
	}

	#endregion

	#region WithCollectionPath Tests

	[Fact]
	public void WithCollectionPath_CreatesNewPositionWithUpdatedPath()
	{
		// Arrange
		var original = FirestoreCdcPosition.Now(TestCollectionPath);
		const string newPath = "new-collection";

		// Act
		var updated = original.WithCollectionPath(newPath);

		// Assert
		updated.CollectionPath.ShouldBe(newPath);
		updated.UpdateTime.ShouldBe(original.UpdateTime);
		updated.LastDocumentId.ShouldBe(original.LastDocumentId);
	}

	#endregion

	#region IsAfterPosition Tests

	[Fact]
	public void IsAfterPosition_ReturnsTrue_WhenPositionHasNoUpdateTime()
	{
		// Arrange
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Act
		var result = position.IsAfterPosition(DateTimeOffset.UtcNow, TestDocumentId);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsAfterPosition_ReturnsTrue_WhenDocUpdateTimeIsGreater()
	{
		// Arrange
		var positionTime = DateTimeOffset.UtcNow;
		var position = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, positionTime, TestDocumentId);
		var laterTime = positionTime.AddSeconds(1);

		// Act
		var result = position.IsAfterPosition(laterTime, "other-doc");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsAfterPosition_ReturnsFalse_WhenDocUpdateTimeIsLess()
	{
		// Arrange
		var positionTime = DateTimeOffset.UtcNow;
		var position = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, positionTime, TestDocumentId);
		var earlierTime = positionTime.AddSeconds(-1);

		// Act
		var result = position.IsAfterPosition(earlierTime, "other-doc");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsAfterPosition_UsesDocumentIdForTieBreaker_WhenTimesAreEqual()
	{
		// Arrange
		var positionTime = DateTimeOffset.UtcNow;
		var position = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, positionTime, "aaa");

		// Act - "zzz" > "aaa" alphabetically
		var result = position.IsAfterPosition(positionTime, "zzz");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsAfterPosition_ReturnsFalse_WhenTimesAreEqual_AndDocIdIsLess()
	{
		// Arrange
		var positionTime = DateTimeOffset.UtcNow;
		var position = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, positionTime, "zzz");

		// Act - "aaa" < "zzz" alphabetically
		var result = position.IsAfterPosition(positionTime, "aaa");

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region Serialization Tests

	[Fact]
	public void ToBase64_And_FromBase64_RoundTrips()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;
		var original = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);

		// Act
		var base64 = original.ToBase64();
		var restored = FirestoreCdcPosition.FromBase64(base64);

		// Assert
		restored.CollectionPath.ShouldBe(original.CollectionPath);
		// Compare with tolerance for DateTimeOffset serialization
		restored.UpdateTime.ShouldNotBeNull();
		restored.LastDocumentId.ShouldBe(original.LastDocumentId);
	}

	[Fact]
	public void ToBytes_And_FromBytes_RoundTrips()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;
		var original = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);

		// Act
		var bytes = original.ToBytes();
		var restored = FirestoreCdcPosition.FromBytes(bytes);

		// Assert
		restored.CollectionPath.ShouldBe(original.CollectionPath);
		restored.LastDocumentId.ShouldBe(original.LastDocumentId);
	}

	[Fact]
	public void FromBytes_ThrowsArgumentNullException_WhenBytesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => FirestoreCdcPosition.FromBytes(null!));
	}

	[Fact]
	public void FromBase64_ThrowsArgumentException_WhenInputIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => FirestoreCdcPosition.FromBase64(null!));
	}

	[Fact]
	public void FromBase64_ThrowsArgumentException_WhenInputIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => FirestoreCdcPosition.FromBase64(""));
	}

	[Fact]
	public void TryFromBase64_ReturnsFalse_WhenInputIsNull()
	{
		// Act
		var result = FirestoreCdcPosition.TryFromBase64(null, out var position);

		// Assert
		result.ShouldBeFalse();
		position.ShouldBeNull();
	}

	[Fact]
	public void TryFromBase64_ReturnsFalse_WhenInputIsEmpty()
	{
		// Act
		var result = FirestoreCdcPosition.TryFromBase64("", out var position);

		// Assert
		result.ShouldBeFalse();
		position.ShouldBeNull();
	}

	[Fact]
	public void TryFromBase64_ReturnsFalse_WhenInputIsInvalidBase64()
	{
		// Act
		var result = FirestoreCdcPosition.TryFromBase64("not-valid-base64!!!", out var position);

		// Assert
		result.ShouldBeFalse();
		position.ShouldBeNull();
	}

	[Fact]
	public void TryFromBase64_ReturnsTrue_WhenInputIsValid()
	{
		// Arrange
		var original = FirestoreCdcPosition.Now(TestCollectionPath);
		var base64 = original.ToBase64();

		// Act
		var result = FirestoreCdcPosition.TryFromBase64(base64, out var position);

		// Assert
		result.ShouldBeTrue();
		position.ShouldNotBeNull();
		position.CollectionPath.ShouldBe(TestCollectionPath);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_ReturnsTrue_ForEqualPositions()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;
		var position1 = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);
		var position2 = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);

		// Act & Assert
		position1.Equals(position2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_ReturnsFalse_ForDifferentCollectionPaths()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;
		var position1 = FirestoreCdcPosition.FromUpdateTime("path1", updateTime, TestDocumentId);
		var position2 = FirestoreCdcPosition.FromUpdateTime("path2", updateTime, TestDocumentId);

		// Act & Assert
		position1.Equals(position2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnsFalse_WhenComparedToNull()
	{
		// Arrange
		var position = FirestoreCdcPosition.Now(TestCollectionPath);

		// Act & Assert
		position.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void OperatorEquals_ReturnsTrue_ForEqualPositions()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;
		var position1 = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);
		var position2 = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);

		// Act & Assert
		(position1 == position2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorNotEquals_ReturnsTrue_ForDifferentPositions()
	{
		// Arrange
		var position1 = FirestoreCdcPosition.Beginning("path1");
		var position2 = FirestoreCdcPosition.Beginning("path2");

		// Act & Assert
		(position1 != position2).ShouldBeTrue();
	}

	[Fact]
	public void GetHashCode_ReturnsSameValue_ForEqualPositions()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;
		var position1 = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);
		var position2 = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);

		// Act & Assert
		position1.GetHashCode().ShouldBe(position2.GetHashCode());
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsBeginning_ForBeginningPosition()
	{
		// Arrange
		var position = FirestoreCdcPosition.Beginning(TestCollectionPath);

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldBe("Beginning");
	}

	[Fact]
	public void ToString_IncludesPosition_ForValidPosition()
	{
		// Arrange
		var updateTime = DateTimeOffset.UtcNow;
		var position = FirestoreCdcPosition.FromUpdateTime(TestCollectionPath, updateTime, TestDocumentId);

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldContain("Position");
		result.ShouldContain(TestDocumentId);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(FirestoreCdcPosition).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(FirestoreCdcPosition).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIEquatable()
	{
		// Assert
		typeof(IEquatable<FirestoreCdcPosition>).IsAssignableFrom(typeof(FirestoreCdcPosition)).ShouldBeTrue();
	}

	#endregion
}
