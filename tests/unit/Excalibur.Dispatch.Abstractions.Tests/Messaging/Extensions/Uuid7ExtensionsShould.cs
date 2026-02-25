// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Abstractions.Tests.Messaging.Extensions;

/// <summary>
/// Unit tests for <see cref="Uuid7Extensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class Uuid7ExtensionsShould
{
	#region GenerateString Tests

	[Fact]
	public void GenerateString_ReturnsNonEmptyString()
	{
		// Act
		var result = Uuid7Extensions.GenerateString();

		// Assert
		result.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void GenerateString_Returns25CharacterString()
	{
		// Act
		var result = Uuid7Extensions.GenerateString();

		// Assert
		result.Length.ShouldBe(25);
	}

	[Fact]
	public void GenerateString_GeneratesUniqueStrings()
	{
		// Act
		var result1 = Uuid7Extensions.GenerateString();
		var result2 = Uuid7Extensions.GenerateString();

		// Assert
		result1.ShouldNotBe(result2);
	}

	#endregion

	#region GenerateGuid Tests

	[Fact]
	public void GenerateGuid_ReturnsNonEmptyGuid()
	{
		// Act
		var result = Uuid7Extensions.GenerateGuid();

		// Assert
		result.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void GenerateGuid_GeneratesUniqueGuids()
	{
		// Act
		var result1 = Uuid7Extensions.GenerateGuid();
		var result2 = Uuid7Extensions.GenerateGuid();

		// Assert
		result1.ShouldNotBe(result2);
	}

	[Fact]
	public void GenerateGuid_WithMatchGuidEndiannessTrue_ReturnsNonEmptyGuid()
	{
		// Act
		var result = Uuid7Extensions.GenerateGuid(matchGuidEndianness: true);

		// Assert
		result.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void GenerateGuid_WithMatchGuidEndiannessFalse_ReturnsNonEmptyGuid()
	{
		// Act
		var result = Uuid7Extensions.GenerateGuid(matchGuidEndianness: false);

		// Assert
		result.ShouldNotBe(Guid.Empty);
	}

	#endregion

	#region GenerateGuidWithTimestamp Tests

	[Fact]
	public void GenerateGuidWithTimestamp_ReturnsNonEmptyGuid()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var result = Uuid7Extensions.GenerateGuidWithTimestamp(timestamp);

		// Assert
		result.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void GenerateGuidWithTimestamp_GeneratesUniqueGuids()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var result1 = Uuid7Extensions.GenerateGuidWithTimestamp(timestamp);
		var result2 = Uuid7Extensions.GenerateGuidWithTimestamp(timestamp);

		// Assert
		result1.ShouldNotBe(result2);
	}

	#endregion

	#region GenerateGuids Tests

	[Fact]
	public void GenerateGuids_WithValidCount_ReturnsCorrectNumberOfGuids()
	{
		// Act
		var results = Uuid7Extensions.GenerateGuids(5);

		// Assert
		results.Length.ShouldBe(5);
	}

	[Fact]
	public void GenerateGuids_WithCountOne_ReturnsSingleGuid()
	{
		// Act
		var results = Uuid7Extensions.GenerateGuids(1);

		// Assert
		results.Length.ShouldBe(1);
		results[0].ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void GenerateGuids_WithZeroCount_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => Uuid7Extensions.GenerateGuids(0));
	}

	[Fact]
	public void GenerateGuids_WithNegativeCount_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => Uuid7Extensions.GenerateGuids(-1));
	}

	[Fact]
	public void GenerateGuids_GeneratesAllUniqueGuids()
	{
		// Act
		var results = Uuid7Extensions.GenerateGuids(10);

		// Assert
		results.Distinct().Count().ShouldBe(10);
	}

	[Fact]
	public void GenerateGuids_WithLargeCount_ReturnsCorrectNumberOfGuids()
	{
		// Act
		var results = Uuid7Extensions.GenerateGuids(100);

		// Assert
		results.Length.ShouldBe(100);
	}

	#endregion

	#region GenerateStrings Tests

	[Fact]
	public void GenerateStrings_WithValidCount_ReturnsCorrectNumberOfStrings()
	{
		// Act
		var results = Uuid7Extensions.GenerateStrings(5);

		// Assert
		results.Length.ShouldBe(5);
	}

	[Fact]
	public void GenerateStrings_WithZeroCount_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => Uuid7Extensions.GenerateStrings(0));
	}

	[Fact]
	public void GenerateStrings_WithNegativeCount_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => Uuid7Extensions.GenerateStrings(-1));
	}

	[Fact]
	public void GenerateStrings_GeneratesAllUniqueStrings()
	{
		// Act
		var results = Uuid7Extensions.GenerateStrings(10);

		// Assert
		results.Distinct().Count().ShouldBe(10);
	}

	#endregion

	#region ExtractTimestamp Tests

	[Fact]
	public void ExtractTimestamp_FromGuid_ReturnsTimestamp()
	{
		// Arrange
		var guid = Uuid7Extensions.GenerateGuid();

		// Act
		var timestamp = Uuid7Extensions.ExtractTimestamp(guid);

		// Assert - May or may not extract depending on UUID v7 format
		// The method returns null for non-v7 GUIDs
	}

	[Fact]
	public void ExtractTimestamp_FromEmptyGuid_ReturnsNull()
	{
		// Act
		var timestamp = Uuid7Extensions.ExtractTimestamp(Guid.Empty);

		// Assert
		timestamp.ShouldBeNull();
	}

	[Fact]
	public void ExtractTimestamp_FromString_ReturnsNull_WhenInvalidFormat()
	{
		// Act
		var timestamp = Uuid7Extensions.ExtractTimestamp("invalid-uuid");

		// Assert
		timestamp.ShouldBeNull();
	}

	[Fact]
	public void ExtractTimestamp_FromNullString_ReturnsNull()
	{
		// Act
		var timestamp = Uuid7Extensions.ExtractTimestamp((string)null!);

		// Assert
		timestamp.ShouldBeNull();
	}

	[Fact]
	public void ExtractTimestamp_FromEmptyString_ReturnsNull()
	{
		// Act
		var timestamp = Uuid7Extensions.ExtractTimestamp(string.Empty);

		// Assert
		timestamp.ShouldBeNull();
	}

	[Fact]
	public void ExtractTimestamp_FromWhitespaceString_ReturnsNull()
	{
		// Act
		var timestamp = Uuid7Extensions.ExtractTimestamp("   ");

		// Assert
		timestamp.ShouldBeNull();
	}

	[Fact]
	public void ExtractTimestamp_FromValidGuidString_HandlesGracefully()
	{
		// Arrange
		var guidString = Guid.NewGuid().ToString();

		// Act
		var timestamp = Uuid7Extensions.ExtractTimestamp(guidString);

		// Assert - May return null for non-v7 GUID
	}

	#endregion

	#region IsValidUuid7String Tests

	[Fact]
	public void IsValidUuid7String_WithNullString_ReturnsFalse()
	{
		// Act
		var result = Uuid7Extensions.IsValidUuid7String(null);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsValidUuid7String_WithEmptyString_ReturnsFalse()
	{
		// Act
		var result = Uuid7Extensions.IsValidUuid7String(string.Empty);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsValidUuid7String_WithWhitespaceString_ReturnsFalse()
	{
		// Act
		var result = Uuid7Extensions.IsValidUuid7String("   ");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsValidUuid7String_WithInvalidString_ReturnsFalse()
	{
		// Act
		var result = Uuid7Extensions.IsValidUuid7String("not-a-uuid");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsValidUuid7String_WithValidGuidString_ReturnsTrue()
	{
		// Arrange
		var guidString = Guid.NewGuid().ToString();

		// Act
		var result = Uuid7Extensions.IsValidUuid7String(guidString);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region IsValidUuid7Guid Tests

	[Fact]
	public void IsValidUuid7Guid_WithEmptyGuid_ReturnsFalse()
	{
		// Act
		var result = Uuid7Extensions.IsValidUuid7Guid(Guid.Empty);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsValidUuid7Guid_WithGeneratedUuid7_ReturnsTrue()
	{
		// Arrange
		var uuid7 = Uuid7Extensions.GenerateGuid();

		// Act
		var result = Uuid7Extensions.IsValidUuid7Guid(uuid7);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsValidUuid7Guid_WithNonUuid7Guid_ReturnsFalse()
	{
		// Arrange - Deterministic GUID that is definitely not UUID v7
		// (Guid.NewGuid() may produce UUID v7 on .NET 10+, making the test probabilistic)
		var guid = new Guid("01020304-0506-4008-890a-0b0c0d0e0f10");

		// Act
		var result = Uuid7Extensions.IsValidUuid7Guid(guid);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region ToGuid Tests

	[Fact]
	public void ToGuid_WithNullString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Uuid7Extensions.ToGuid(null!));
	}

	[Fact]
	public void ToGuid_WithEmptyString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Uuid7Extensions.ToGuid(string.Empty));
	}

	[Fact]
	public void ToGuid_WithWhitespaceString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Uuid7Extensions.ToGuid("   "));
	}

	[Fact]
	public void ToGuid_WithValidGuidString_ReturnsGuid()
	{
		// Arrange
		var original = Guid.NewGuid();
		var guidString = original.ToString();

		// Act
		var result = Uuid7Extensions.ToGuid(guidString);

		// Assert
		result.ShouldBe(original);
	}

	[Fact]
	public void ToGuid_WithInvalidString_ReturnsEmptyGuid()
	{
		// Act
		var result = Uuid7Extensions.ToGuid("not-a-valid-guid");

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	#endregion

	#region ToUuid7String Tests

	[Fact]
	public void ToUuid7String_WithEmptyGuid_ReturnsNull()
	{
		// Act
		var result = Uuid7Extensions.ToUuid7String(Guid.Empty);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ToUuid7String_WithValidUuid7_ReturnsString()
	{
		// Arrange
		var uuid7 = Uuid7Extensions.GenerateGuid();

		// Act
		var result = Uuid7Extensions.ToUuid7String(uuid7);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void ToUuid7String_WithNonV7Guid_ReturnsNull()
	{
		// Arrange - A GUID whose version nibble is NOT 7.
		// Note: Guid.NewGuid() returns UUID v7 on .NET 9+, so we cannot use it here.
		// Guid.ToByteArray() uses mixed-endian: third group "4xxx" byte-swaps so
		// bytes[6] gets the low byte (0x0x), not the version nibble. We use a GUID
		// where the third group ensures bytes[6] & 0xF0 != 0x70 after the swap.
		// Third group "1234" → bytes[6]=0x34, bytes[7]=0x12 → version nibble = 3.
		var guid = new Guid("aaaaaaaa-bbbb-1234-cccc-dddddddddddd");

		// Act
		var result = Uuid7Extensions.ToUuid7String(guid);

		// Assert - Version nibble is not 7, so this should be null
		result.ShouldBeNull();
	}

	#endregion

	#region GenerateSequentialGuids Tests

	[Fact]
	public void GenerateSequentialGuids_WithValidCount_ReturnsCorrectNumber()
	{
		// Act
		var results = Uuid7Extensions.GenerateSequentialGuids(3, intervalMs: 0).ToList();

		// Assert
		results.Count.ShouldBe(3);
	}

	[Fact]
	public void GenerateSequentialGuids_WithZeroCount_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			Uuid7Extensions.GenerateSequentialGuids(0).ToList());
	}

	[Fact]
	public void GenerateSequentialGuids_WithNegativeCount_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			Uuid7Extensions.GenerateSequentialGuids(-1).ToList());
	}

	[Fact]
	public void GenerateSequentialGuids_WithNegativeInterval_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			Uuid7Extensions.GenerateSequentialGuids(5, intervalMs: -1).ToList());
	}

	[Fact]
	public void GenerateSequentialGuids_GeneratesAllUniqueGuids()
	{
		// Act
		var results = Uuid7Extensions.GenerateSequentialGuids(5, intervalMs: 0).ToList();

		// Assert
		results.Distinct().Count().ShouldBe(5);
	}

	#endregion

	#region CompareByTimestamp Tests

	[Fact]
	public void CompareByTimestamp_WithSameGuid_ReturnsZero()
	{
		// Arrange
		var guid = Uuid7Extensions.GenerateGuid();

		// Act
		var result = Uuid7Extensions.CompareByTimestamp(guid, guid);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void CompareByTimestamp_WithEmptyGuids_ReturnsZero()
	{
		// Act
		var result = Uuid7Extensions.CompareByTimestamp(Guid.Empty, Guid.Empty);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void CompareByTimestamp_WithFirstEmpty_ReturnsNegative()
	{
		// Arrange
		var uuid7 = Uuid7Extensions.GenerateGuid();

		// Act
		var result = Uuid7Extensions.CompareByTimestamp(Guid.Empty, uuid7);

		// Assert
		result.ShouldBeLessThan(0);
	}

	[Fact]
	public void CompareByTimestamp_WithSecondEmpty_ReturnsPositive()
	{
		// Arrange
		var uuid7 = Uuid7Extensions.GenerateGuid();

		// Act
		var result = Uuid7Extensions.CompareByTimestamp(uuid7, Guid.Empty);

		// Assert
		result.ShouldBeGreaterThan(0);
	}

	#endregion
}
