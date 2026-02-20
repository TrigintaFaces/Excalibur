// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.DataAccess;

/// <summary>
/// Unit tests for <see cref="ChangePosition"/> and <see cref="TokenChangePosition"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DataAccess")]
[Trait("Priority", "0")]
public sealed class ChangePositionShould
{
	#region TokenChangePosition Constructor Tests

	[Fact]
	public void TokenChangePosition_Constructor_SetsToken()
	{
		// Act
		var position = new TokenChangePosition("test-token");

		// Assert
		position.Token.ShouldBe("test-token");
	}

	[Fact]
	public void TokenChangePosition_Constructor_WithNullToken_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new TokenChangePosition(null!));
	}

	[Fact]
	public void TokenChangePosition_Constructor_WithTimestamp_SetsTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var position = new TokenChangePosition("test-token", timestamp);

		// Assert
		position.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void TokenChangePosition_Constructor_WithoutTimestamp_TimestampIsNull()
	{
		// Act
		var position = new TokenChangePosition("test-token");

		// Assert
		position.Timestamp.ShouldBeNull();
	}

	#endregion

	#region TokenChangePosition.Empty Tests

	[Fact]
	public void TokenChangePosition_Empty_HasEmptyToken()
	{
		// Assert
		TokenChangePosition.Empty.Token.ShouldBe(string.Empty);
	}

	[Fact]
	public void TokenChangePosition_Empty_IsNotValid()
	{
		// Assert
		TokenChangePosition.Empty.IsValid.ShouldBeFalse();
	}

	#endregion

	#region IsValid Tests

	[Fact]
	public void IsValid_WithNonEmptyToken_ReturnsTrue()
	{
		// Arrange
		var position = new TokenChangePosition("valid-token");

		// Act & Assert
		position.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void IsValid_WithEmptyToken_ReturnsFalse()
	{
		// Arrange
		var position = new TokenChangePosition(string.Empty);

		// Act & Assert
		position.IsValid.ShouldBeFalse();
	}

	#endregion

	#region ToToken Tests

	[Fact]
	public void ToToken_ReturnsToken()
	{
		// Arrange
		var position = new TokenChangePosition("my-token-value");

		// Act
		var token = position.ToToken();

		// Assert
		token.ShouldBe("my-token-value");
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsToken()
	{
		// Arrange
		var position = new TokenChangePosition("string-representation");

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldBe("string-representation");
	}

	#endregion

	#region Parse Tests

	[Fact]
	public void Parse_CreatesTokenChangePosition()
	{
		// Act
		var position = TokenChangePosition.Parse("parsed-token");

		// Assert
		position.Token.ShouldBe("parsed-token");
	}

	[Fact]
	public void Parse_ReturnsValidPosition()
	{
		// Act
		var position = TokenChangePosition.Parse("valid-token");

		// Assert
		position.IsValid.ShouldBeTrue();
	}

	#endregion

	#region TryParse Tests

	[Fact]
	public void TryParse_WithValidToken_ReturnsTrue()
	{
		// Act
		var success = TokenChangePosition.TryParse("valid-token", out var position);

		// Assert
		success.ShouldBeTrue();
		position.Token.ShouldBe("valid-token");
	}

	[Fact]
	public void TryParse_WithNullToken_ReturnsFalse()
	{
		// Act
		var success = TokenChangePosition.TryParse(null, out var position);

		// Assert
		success.ShouldBeFalse();
		position.ShouldBe(TokenChangePosition.Empty);
	}

	[Fact]
	public void TryParse_WithEmptyToken_ReturnsFalse()
	{
		// Act
		var success = TokenChangePosition.TryParse(string.Empty, out var position);

		// Assert
		success.ShouldBeFalse();
		position.ShouldBe(TokenChangePosition.Empty);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameToken_ReturnsTrue()
	{
		// Arrange
		var position1 = new TokenChangePosition("same-token");
		var position2 = new TokenChangePosition("same-token");

		// Act & Assert
		position1.Equals(position2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentToken_ReturnsFalse()
	{
		// Arrange
		var position1 = new TokenChangePosition("token-a");
		var position2 = new TokenChangePosition("token-b");

		// Act & Assert
		position1.Equals(position2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");

		// Act & Assert
		position.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentType_ReturnsFalse()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");
		var otherPosition = new TestChangePosition();

		// Act & Assert
		position.Equals(otherPosition).ShouldBeFalse();
	}

	[Fact]
	public void ObjectEquals_WithSameToken_ReturnsTrue()
	{
		// Arrange
		var position1 = new TokenChangePosition("same-token");
		object position2 = new TokenChangePosition("same-token");

		// Act & Assert
		position1.Equals(position2).ShouldBeTrue();
	}

	[Fact]
	public void ObjectEquals_WithNonChangePosition_ReturnsFalse()
	{
		// Arrange
		var position = new TokenChangePosition("test-token");

		// Act & Assert
		position.Equals("test-token").ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_WithSameToken_ReturnsSameHash()
	{
		// Arrange
		var position1 = new TokenChangePosition("same-token");
		var position2 = new TokenChangePosition("same-token");

		// Act & Assert
		position1.GetHashCode().ShouldBe(position2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithDifferentToken_ReturnsDifferentHash()
	{
		// Arrange
		var position1 = new TokenChangePosition("token-a");
		var position2 = new TokenChangePosition("token-b");

		// Act & Assert
		position1.GetHashCode().ShouldNotBe(position2.GetHashCode());
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void OperatorEquals_WithSameToken_ReturnsTrue()
	{
		// Arrange
		var position1 = new TokenChangePosition("same-token");
		var position2 = new TokenChangePosition("same-token");

		// Act & Assert
		(position1 == position2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorEquals_WithDifferentToken_ReturnsFalse()
	{
		// Arrange
		var position1 = new TokenChangePosition("token-a");
		var position2 = new TokenChangePosition("token-b");

		// Act & Assert
		(position1 == position2).ShouldBeFalse();
	}

	[Fact]
	public void OperatorEquals_WithBothNull_ReturnsTrue()
	{
		// Arrange
		ChangePosition? position1 = null;
		ChangePosition? position2 = null;

		// Act & Assert
		(position1 == position2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorEquals_WithOneNull_ReturnsFalse()
	{
		// Arrange
		var position1 = new TokenChangePosition("test-token");
		ChangePosition? position2 = null;

		// Act & Assert
		(position1 == position2).ShouldBeFalse();
	}

	[Fact]
	public void OperatorNotEquals_WithDifferentToken_ReturnsTrue()
	{
		// Arrange
		var position1 = new TokenChangePosition("token-a");
		var position2 = new TokenChangePosition("token-b");

		// Act & Assert
		(position1 != position2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorNotEquals_WithSameToken_ReturnsFalse()
	{
		// Arrange
		var position1 = new TokenChangePosition("same-token");
		var position2 = new TokenChangePosition("same-token");

		// Act & Assert
		(position1 != position2).ShouldBeFalse();
	}

	#endregion

	#region Test Helper Types

	private sealed class TestChangePosition : ChangePosition
	{
		public override bool IsValid => true;

		public override string ToToken() => "test";

		public override bool Equals(ChangePosition? other) => other is TestChangePosition;

		public override int GetHashCode() => 0;
	}

	#endregion
}
