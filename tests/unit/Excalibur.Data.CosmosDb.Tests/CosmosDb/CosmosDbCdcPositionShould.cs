// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Data.CosmosDb;
namespace Excalibur.Data.Tests.CosmosDb.Cdc;

/// <summary>
/// Unit tests for <see cref="CosmosDbCdcPosition"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "CosmosDb")]
public sealed class CosmosDbCdcPositionShould : UnitTestBase
{
	[Fact]
	public void CreateBeginningPosition()
	{
		// Act
		var position = CosmosDbCdcPosition.Beginning();

		// Assert
		position.ContinuationToken.ShouldBeNull();
		position.Timestamp.ShouldBeNull();
		position.IsBeginning.ShouldBeTrue();
		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void CreateNowPosition()
	{
		// Act
		var before = DateTimeOffset.UtcNow;
		var position = CosmosDbCdcPosition.Now();
		var after = DateTimeOffset.UtcNow;

		// Assert
		position.ContinuationToken.ShouldBeNull();
		_ = position.Timestamp.ShouldNotBeNull();
		position.Timestamp.Value.ShouldBeGreaterThanOrEqualTo(before);
		position.Timestamp.Value.ShouldBeLessThanOrEqualTo(after);
		position.IsBeginning.ShouldBeFalse();
		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void CreateFromTimestamp()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var position = CosmosDbCdcPosition.FromTimestamp(timestamp);

		// Assert
		position.ContinuationToken.ShouldBeNull();
		position.Timestamp.ShouldBe(timestamp);
		position.IsBeginning.ShouldBeFalse();
		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void CreateFromContinuationToken()
	{
		// Arrange
		var token = "test-continuation-token-12345";

		// Act
		var position = CosmosDbCdcPosition.FromContinuationToken(token);

		// Assert
		position.ContinuationToken.ShouldBe(token);
		position.Timestamp.ShouldBeNull();
		position.IsBeginning.ShouldBeFalse();
		position.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenCreatingFromInvalidToken(string? invalidToken)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => CosmosDbCdcPosition.FromContinuationToken(invalidToken));
	}

	[Fact]
	public void RoundTripBeginningThroughBase64()
	{
		// Arrange
		var original = CosmosDbCdcPosition.Beginning();

		// Act
		var base64 = original.ToBase64();
		var parsed = CosmosDbCdcPosition.FromBase64(base64);

		// Assert
		parsed.IsBeginning.ShouldBeTrue();
		parsed.ShouldBe(original);
	}

	[Fact]
	public void RoundTripTimestampThroughBase64()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
		var original = CosmosDbCdcPosition.FromTimestamp(timestamp);

		// Act
		var base64 = original.ToBase64();
		var parsed = CosmosDbCdcPosition.FromBase64(base64);

		// Assert
		parsed.Timestamp.ShouldBe(timestamp);
		parsed.ShouldBe(original);
	}

	[Fact]
	public void RoundTripContinuationTokenThroughBase64()
	{
		// Arrange
		var token = "test-token-abc123";
		var original = CosmosDbCdcPosition.FromContinuationToken(token);

		// Act
		var base64 = original.ToBase64();
		var parsed = CosmosDbCdcPosition.FromBase64(base64);

		// Assert
		parsed.ContinuationToken.ShouldBe(token);
		parsed.ShouldBe(original);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenParsingInvalidBase64(string? invalidBase64)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => CosmosDbCdcPosition.FromBase64(invalidBase64));
	}

	[Fact]
	public void ThrowWhenParsingMalformedBase64()
	{
		// Arrange - valid base64 but invalid position format
		var malformedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("X:invalid"));

		// Act & Assert
		_ = Should.Throw<FormatException>(() => CosmosDbCdcPosition.FromBase64(malformedBase64));
	}

	[Fact]
	public void TryFromBase64ReturnsTrueForValidInput()
	{
		// Arrange
		var original = CosmosDbCdcPosition.FromContinuationToken("test-token");
		var base64 = original.ToBase64();

		// Act
		var success = CosmosDbCdcPosition.TryFromBase64(base64, out var position);

		// Assert
		success.ShouldBeTrue();
		position.ShouldBe(original);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TryFromBase64ReturnsFalseForInvalidInput(string? invalidBase64)
	{
		// Act
		var success = CosmosDbCdcPosition.TryFromBase64(invalidBase64, out var position);

		// Assert
		success.ShouldBeFalse();
		position.IsBeginning.ShouldBeTrue();
	}

	[Fact]
	public void TryFromBase64ReturnsFalseForMalformedInput()
	{
		// Arrange
		var malformedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("X:invalid"));

		// Act
		var success = CosmosDbCdcPosition.TryFromBase64(malformedBase64, out var position);

		// Assert
		success.ShouldBeFalse();
		position.IsBeginning.ShouldBeTrue();
	}

	[Fact]
	public void ImplementEquality()
	{
		// Arrange
		var token1 = CosmosDbCdcPosition.FromContinuationToken("token-abc");
		var token2 = CosmosDbCdcPosition.FromContinuationToken("token-abc");
		var token3 = CosmosDbCdcPosition.FromContinuationToken("token-xyz");

		// Assert
		token1.ShouldBe(token2);
		token1.ShouldNotBe(token3);
		(token1 == token2).ShouldBeTrue();
		(token1 != token3).ShouldBeTrue();
	}

	[Fact]
	public void HandleNullInEqualityOperator()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();
		CosmosDbCdcPosition? nullPosition = null;

		// Assert
		(position == nullPosition).ShouldBeFalse();
		(nullPosition == position).ShouldBeFalse();
		(nullPosition == null).ShouldBeTrue();
		(position != nullPosition).ShouldBeTrue();
	}

	[Fact]
	public void ProvideConsistentHashCodes()
	{
		// Arrange
		var token1 = CosmosDbCdcPosition.FromContinuationToken("token-abc");
		var token2 = CosmosDbCdcPosition.FromContinuationToken("token-abc");

		// Assert
		token1.GetHashCode().ShouldBe(token2.GetHashCode());
	}

	[Fact]
	public void FormatBeginningAsString()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldBe("Beginning");
	}

	[Fact]
	public void FormatTimestampAsString()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
		var position = CosmosDbCdcPosition.FromTimestamp(timestamp);

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldStartWith("Timestamp(");
		result.ShouldContain("2025-06-15");
	}

	[Fact]
	public void FormatShortTokenAsString()
	{
		// Arrange
		var shortToken = "short-token";
		var position = CosmosDbCdcPosition.FromContinuationToken(shortToken);

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldBe($"Token({shortToken})");
	}

	[Fact]
	public void TruncateLongTokenInString()
	{
		// Arrange
		var longToken = new string('x', 100);
		var position = CosmosDbCdcPosition.FromContinuationToken(longToken);

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldStartWith("Token(");
		result.ShouldEndWith("...)");
		result.Length.ShouldBeLessThan(longToken.Length);
	}

	[Fact]
	public void ReferenceEqualityReturnsTrueForSameInstance()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();

		// Act & Assert
		position.Equals(position).ShouldBeTrue();
	}

	[Fact]
	public void EqualsReturnsFalseForNull()
	{
		// Arrange
		var position = CosmosDbCdcPosition.Beginning();

		// Act & Assert
		position.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectWorksCorrectly()
	{
		// Arrange
		var position1 = CosmosDbCdcPosition.FromContinuationToken("token");
		var position2 = CosmosDbCdcPosition.FromContinuationToken("token");
		object boxed = position2;
		object notAPosition = "not a position";

		// Assert
		position1.Equals(boxed).ShouldBeTrue();
		position1.Equals(notAPosition).ShouldBeFalse();
	}
}
