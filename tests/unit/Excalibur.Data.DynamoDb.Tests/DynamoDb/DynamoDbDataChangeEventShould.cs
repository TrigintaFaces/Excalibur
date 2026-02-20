// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2.Model;

using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbDataChangeEvent"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify data change event properties and factory methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class DynamoDbDataChangeEventShould
{
	#region Default Value Tests

	[Fact]
	public void ShardId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var evt = new DynamoDbDataChangeEvent();

		// Assert
		evt.ShardId.ShouldBe(string.Empty);
	}

	[Fact]
	public void SequenceNumber_DefaultsToEmptyString()
	{
		// Arrange & Act
		var evt = new DynamoDbDataChangeEvent();

		// Assert
		evt.SequenceNumber.ShouldBe(string.Empty);
	}

	[Fact]
	public void NewImage_DefaultsToNull()
	{
		// Arrange & Act
		var evt = new DynamoDbDataChangeEvent();

		// Assert
		evt.NewImage.ShouldBeNull();
	}

	[Fact]
	public void OldImage_DefaultsToNull()
	{
		// Arrange & Act
		var evt = new DynamoDbDataChangeEvent();

		// Assert
		evt.OldImage.ShouldBeNull();
	}

	[Fact]
	public void Keys_DefaultsToEmptyDictionary()
	{
		// Arrange & Act
		var evt = new DynamoDbDataChangeEvent();

		// Assert
		evt.Keys.ShouldNotBeNull();
		evt.Keys.ShouldBeEmpty();
	}

	[Fact]
	public void EventId_DefaultsToNull()
	{
		// Arrange & Act
		var evt = new DynamoDbDataChangeEvent();

		// Assert
		evt.EventId.ShouldBeNull();
	}

	#endregion

	#region CreateInsert Factory Method Tests

	[Fact]
	public void CreateInsert_SetsChangeTypeToInsert()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01");
		var keys = new Dictionary<string, AttributeValue>();

		// Act
		var evt = DynamoDbDataChangeEvent.CreateInsert(
			position, "shard-1", "seq-123", keys, null, DateTimeOffset.UtcNow, "event-1");

		// Assert
		evt.ChangeType.ShouldBe(DynamoDbDataChangeType.Insert);
	}

	[Fact]
	public void CreateInsert_SetsAllProperties()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01");
		var keys = new Dictionary<string, AttributeValue>
		{
			["pk"] = new() { S = "test-key" }
		};
		var newImage = new Dictionary<string, AttributeValue>
		{
			["pk"] = new() { S = "test-key" },
			["data"] = new() { S = "test-data" }
		};
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = DynamoDbDataChangeEvent.CreateInsert(
			position, "shard-1", "seq-123", keys, newImage, timestamp, "event-1");

		// Assert
		evt.Position.ShouldBe(position);
		evt.ShardId.ShouldBe("shard-1");
		evt.SequenceNumber.ShouldBe("seq-123");
		evt.Keys.ShouldBe(keys);
		evt.NewImage.ShouldBe(newImage);
		evt.OldImage.ShouldBeNull();
		evt.Timestamp.ShouldBe(timestamp);
		evt.EventId.ShouldBe("event-1");
	}

	#endregion

	#region CreateModify Factory Method Tests

	[Fact]
	public void CreateModify_SetsChangeTypeToModify()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01");
		var keys = new Dictionary<string, AttributeValue>();

		// Act
		var evt = DynamoDbDataChangeEvent.CreateModify(
			position, "shard-1", "seq-123", keys, null, null, DateTimeOffset.UtcNow, "event-1");

		// Assert
		evt.ChangeType.ShouldBe(DynamoDbDataChangeType.Modify);
	}

	[Fact]
	public void CreateModify_SetsAllProperties()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01");
		var keys = new Dictionary<string, AttributeValue>
		{
			["pk"] = new() { S = "test-key" }
		};
		var newImage = new Dictionary<string, AttributeValue>
		{
			["pk"] = new() { S = "test-key" },
			["data"] = new() { S = "new-data" }
		};
		var oldImage = new Dictionary<string, AttributeValue>
		{
			["pk"] = new() { S = "test-key" },
			["data"] = new() { S = "old-data" }
		};
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = DynamoDbDataChangeEvent.CreateModify(
			position, "shard-1", "seq-123", keys, newImage, oldImage, timestamp, "event-1");

		// Assert
		evt.Position.ShouldBe(position);
		evt.ShardId.ShouldBe("shard-1");
		evt.SequenceNumber.ShouldBe("seq-123");
		evt.Keys.ShouldBe(keys);
		evt.NewImage.ShouldBe(newImage);
		evt.OldImage.ShouldBe(oldImage);
		evt.Timestamp.ShouldBe(timestamp);
		evt.EventId.ShouldBe("event-1");
	}

	#endregion

	#region CreateRemove Factory Method Tests

	[Fact]
	public void CreateRemove_SetsChangeTypeToRemove()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01");
		var keys = new Dictionary<string, AttributeValue>();

		// Act
		var evt = DynamoDbDataChangeEvent.CreateRemove(
			position, "shard-1", "seq-123", keys, null, DateTimeOffset.UtcNow, "event-1");

		// Assert
		evt.ChangeType.ShouldBe(DynamoDbDataChangeType.Remove);
	}

	[Fact]
	public void CreateRemove_SetsAllProperties()
	{
		// Arrange
		var position = DynamoDbCdcPosition.Beginning("arn:aws:dynamodb:us-east-1:123456789012:table/TestTable/stream/2024-01-01");
		var keys = new Dictionary<string, AttributeValue>
		{
			["pk"] = new() { S = "test-key" }
		};
		var oldImage = new Dictionary<string, AttributeValue>
		{
			["pk"] = new() { S = "test-key" },
			["data"] = new() { S = "deleted-data" }
		};
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = DynamoDbDataChangeEvent.CreateRemove(
			position, "shard-1", "seq-123", keys, oldImage, timestamp, "event-1");

		// Assert
		evt.Position.ShouldBe(position);
		evt.ShardId.ShouldBe("shard-1");
		evt.SequenceNumber.ShouldBe("seq-123");
		evt.Keys.ShouldBe(keys);
		evt.NewImage.ShouldBeNull();
		evt.OldImage.ShouldBe(oldImage);
		evt.Timestamp.ShouldBe(timestamp);
		evt.EventId.ShouldBe("event-1");
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(DynamoDbDataChangeEvent).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbDataChangeEvent).IsPublic.ShouldBeTrue();
	}

	#endregion
}
