// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcPosition"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "DynamoDb")]
public sealed class DynamoDbCdcPositionShould : UnitTestBase
{
	private const string TestStreamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2023-01-01T00:00:00.000";

	[Fact]
	public void CreateBeginningPosition()
	{
		// Act
		var position = DynamoDbCdcPosition.Beginning(TestStreamArn);

		// Assert
		position.StreamArn.ShouldBe(TestStreamArn);
		position.ShardPositions.Count.ShouldBe(0);
		position.Timestamp.ShouldBeNull();
		position.IsBeginning.ShouldBeTrue();
		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void CreateNowPosition()
	{
		// Act
		var before = DateTimeOffset.UtcNow;
		var position = DynamoDbCdcPosition.Now(TestStreamArn);
		var after = DateTimeOffset.UtcNow;

		// Assert
		position.StreamArn.ShouldBe(TestStreamArn);
		position.ShardPositions.Count.ShouldBe(0);
		_ = position.Timestamp.ShouldNotBeNull();
		position.Timestamp.Value.ShouldBeGreaterThanOrEqualTo(before);
		position.Timestamp.Value.ShouldBeLessThanOrEqualTo(after);
		position.IsBeginning.ShouldBeFalse();
		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void CreateFromShardPositions()
	{
		// Arrange
		var shardPositions = new Dictionary<string, string>
		{
			["shardId-00000001541374724421-c0e5a16c"] = "49590338271490256608559690538137916016477959906847203330",
			["shardId-00000001541374724422-a1b2c3d4"] = "49590338271490256608559690538137916016477959906847203331"
		};

		// Act
		var position = DynamoDbCdcPosition.FromShardPositions(TestStreamArn, shardPositions);

		// Assert
		position.StreamArn.ShouldBe(TestStreamArn);
		position.ShardPositions.Count.ShouldBe(2);
		position.ShardPositions.ShouldContainKey("shardId-00000001541374724421-c0e5a16c");
		position.ShardPositions.ShouldContainKey("shardId-00000001541374724422-a1b2c3d4");
		_ = position.Timestamp.ShouldNotBeNull();
		position.IsBeginning.ShouldBeFalse();
		position.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenCreatingFromNullShardPositions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			DynamoDbCdcPosition.FromShardPositions(TestStreamArn, null!));
	}

	[Fact]
	public void UpdateShardPosition()
	{
		// Arrange
		var original = DynamoDbCdcPosition.FromShardPositions(
			TestStreamArn,
			new Dictionary<string, string>
			{
				["shard-1"] = "seq-100"
			});

		// Act
		var updated = original.WithShardPosition("shard-1", "seq-200");

		// Assert
		updated.ShardPositions["shard-1"].ShouldBe("seq-200");
		original.ShardPositions["shard-1"].ShouldBe("seq-100"); // Original unchanged
	}

	[Fact]
	public void AddNewShardPosition()
	{
		// Arrange
		var original = DynamoDbCdcPosition.FromShardPositions(
			TestStreamArn,
			new Dictionary<string, string>
			{
				["shard-1"] = "seq-100"
			});

		// Act
		var updated = original.WithShardPosition("shard-2", "seq-200");

		// Assert
		updated.ShardPositions.Count.ShouldBe(2);
		updated.ShardPositions["shard-1"].ShouldBe("seq-100");
		updated.ShardPositions["shard-2"].ShouldBe("seq-200");
	}

	[Fact]
	public void RemoveShard()
	{
		// Arrange
		var original = DynamoDbCdcPosition.FromShardPositions(
			TestStreamArn,
			new Dictionary<string, string>
			{
				["shard-1"] = "seq-100",
				["shard-2"] = "seq-200"
			});

		// Act
		var updated = original.WithoutShard("shard-1");

		// Assert
		updated.ShardPositions.Count.ShouldBe(1);
		updated.ShardPositions.ShouldContainKey("shard-2");
		updated.ShardPositions.ShouldNotContainKey("shard-1");
		original.ShardPositions.Count.ShouldBe(2); // Original unchanged
	}

	[Fact]
	public void RoundTripBeginningThroughBase64()
	{
		// Arrange
		var original = DynamoDbCdcPosition.Beginning(TestStreamArn);

		// Act
		var base64 = original.ToBase64();
		var parsed = DynamoDbCdcPosition.FromBase64(base64);

		// Assert
		parsed.StreamArn.ShouldBe(TestStreamArn);
		parsed.IsBeginning.ShouldBeTrue();
	}

	[Fact]
	public void RoundTripNowPositionThroughBase64()
	{
		// Arrange
		var original = DynamoDbCdcPosition.Now(TestStreamArn);

		// Act
		var base64 = original.ToBase64();
		var parsed = DynamoDbCdcPosition.FromBase64(base64);

		// Assert
		parsed.StreamArn.ShouldBe(TestStreamArn);
		_ = parsed.Timestamp.ShouldNotBeNull();
	}

	[Fact]
	public void RoundTripShardPositionsThroughBase64()
	{
		// Arrange
		var shardPositions = new Dictionary<string, string>
		{
			["shard-1"] = "seq-100",
			["shard-2"] = "seq-200"
		};
		var original = DynamoDbCdcPosition.FromShardPositions(TestStreamArn, shardPositions);

		// Act
		var base64 = original.ToBase64();
		var parsed = DynamoDbCdcPosition.FromBase64(base64);

		// Assert
		parsed.StreamArn.ShouldBe(TestStreamArn);
		parsed.ShardPositions.Count.ShouldBe(2);
		parsed.ShardPositions["shard-1"].ShouldBe("seq-100");
		parsed.ShardPositions["shard-2"].ShouldBe("seq-200");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenParsingInvalidBase64(string? invalidBase64)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => DynamoDbCdcPosition.FromBase64(invalidBase64));
	}

	[Fact]
	public void TryFromBase64ReturnsTrueForValidInput()
	{
		// Arrange
		var original = DynamoDbCdcPosition.Beginning(TestStreamArn);
		var base64 = original.ToBase64();

		// Act
		var success = DynamoDbCdcPosition.TryFromBase64(base64, out var position);

		// Assert
		success.ShouldBeTrue();
		_ = position.ShouldNotBeNull();
		position.StreamArn.ShouldBe(TestStreamArn);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TryFromBase64ReturnsFalseForInvalidInput(string? invalidBase64)
	{
		// Act
		var success = DynamoDbCdcPosition.TryFromBase64(invalidBase64, out var position);

		// Assert
		success.ShouldBeFalse();
		position.ShouldBeNull();
	}

	[Fact]
	public void ImplementEquality()
	{
		// Arrange
		var shardPositions = new Dictionary<string, string>
		{
			["shard-1"] = "seq-100"
		};
		var position1 = DynamoDbCdcPosition.FromShardPositions(TestStreamArn, shardPositions);
		var position2 = DynamoDbCdcPosition.FromShardPositions(TestStreamArn, new Dictionary<string, string>(shardPositions));
		var position3 = DynamoDbCdcPosition.FromShardPositions(TestStreamArn, new Dictionary<string, string>
		{
			["shard-1"] = "seq-200"
		});

		// Assert
		position1.ShouldBe(position2);
		position1.ShouldNotBe(position3);
		(position1 == position2).ShouldBeTrue();
		(position1 != position3).ShouldBeTrue();
	}

	[Fact]
	public void HandleNullInEqualityOperator()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning(TestStreamArn);
		DynamoDbCdcPosition? nullPosition = null;

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
		var shardPositions = new Dictionary<string, string>
		{
			["shard-1"] = "seq-100"
		};
		var position1 = DynamoDbCdcPosition.FromShardPositions(TestStreamArn, shardPositions);
		var position2 = DynamoDbCdcPosition.FromShardPositions(TestStreamArn, new Dictionary<string, string>(shardPositions));

		// Assert
		position1.GetHashCode().ShouldBe(position2.GetHashCode());
	}

	[Fact]
	public void FormatBeginningAsString()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning(TestStreamArn);

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldBe("Beginning");
	}

	[Fact]
	public void FormatNowPositionAsString()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Now(TestStreamArn);

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldStartWith("Latest(");
	}

	[Fact]
	public void FormatShardPositionAsString()
	{
		// Arrange
		var position = DynamoDbCdcPosition.FromShardPositions(
			TestStreamArn,
			new Dictionary<string, string>
			{
				["shard-1"] = "seq-100",
				["shard-2"] = "seq-200"
			});

		// Act
		var result = position.ToString();

		// Assert
		result.ShouldBe("Shards(2)");
	}

	[Fact]
	public void ReferenceEqualityReturnsTrueForSameInstance()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning(TestStreamArn);

		// Act & Assert
		position.Equals(position).ShouldBeTrue();
	}

	[Fact]
	public void EqualsReturnsFalseForNull()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning(TestStreamArn);

		// Act & Assert
		position.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectWorksCorrectly()
	{
		// Arrange
		var position1 = DynamoDbCdcPosition.Beginning(TestStreamArn);
		var position2 = DynamoDbCdcPosition.Beginning(TestStreamArn);
		object boxed = position2;
		object notAPosition = "not a position";

		// Assert
		position1.Equals(boxed).ShouldBeTrue();
		position1.Equals(notAPosition).ShouldBeFalse();
	}

	[Fact]
	public void DifferentStreamArnsAreNotEqual()
	{
		// Arrange
		var position1 = DynamoDbCdcPosition.Beginning(TestStreamArn);
		var position2 = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-west-2:123456789012:table/OtherTable/stream/2023-01-01");

		// Assert
		position1.ShouldNotBe(position2);
	}

	[Fact]
	public void DifferentShardCountsAreNotEqual()
	{
		// Arrange
		var position1 = DynamoDbCdcPosition.FromShardPositions(
			TestStreamArn,
			new Dictionary<string, string> { ["shard-1"] = "seq-100" });
		var position2 = DynamoDbCdcPosition.FromShardPositions(
			TestStreamArn,
			new Dictionary<string, string>
			{
				["shard-1"] = "seq-100",
				["shard-2"] = "seq-200"
			});

		// Assert
		position1.ShouldNotBe(position2);
	}
}
