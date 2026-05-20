// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

namespace Excalibur.Data.DynamoDb.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DynamoDbRepositoryBaseShould : UnitTestBase
{
	#region Constructor Validation Tests

	[Fact]
	public void ThrowWhenClientIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new TestDynamoDbRepository(null!, "TestTable", "pk"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenTableNameIsNullOrWhitespace(string? tableName)
	{
		// Arrange
		var client = A.Fake<IAmazonDynamoDB>();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new TestDynamoDbRepository(client, tableName!, "pk"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenPartitionKeyNameIsNullOrWhitespace(string? partitionKeyName)
	{
		// Arrange
		var client = A.Fake<IAmazonDynamoDB>();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new TestDynamoDbRepository(client, "TestTable", partitionKeyName!));
	}

	[Fact]
	public void ConstructSuccessfully_WithValidParameters()
	{
		// Arrange
		var client = A.Fake<IAmazonDynamoDB>();

		// Act
		var repo = new TestDynamoDbRepository(client, "TestTable", "pk");

		// Assert
		repo.ShouldNotBeNull();
		repo.ExposedTableName.ShouldBe("TestTable");
		repo.ExposedPartitionKeyName.ShouldBe("pk");
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementIDynamoDbRepositoryBase()
	{
		// Arrange
		var client = A.Fake<IAmazonDynamoDB>();

		// Act
		var repo = new TestDynamoDbRepository(client, "TestTable", "pk");

		// Assert
		repo.ShouldBeAssignableTo<IDynamoDbRepositoryBase<SampleDocument>>();
	}

	[Fact]
	public void ImplementIDynamoDbRepositoryBaseQuery()
	{
		// Arrange
		var client = A.Fake<IAmazonDynamoDB>();

		// Act
		var repo = new TestDynamoDbRepository(client, "TestTable", "pk");

		// Assert
		repo.ShouldBeAssignableTo<IDynamoDbRepositoryBaseQuery<SampleDocument>>();
	}

	#endregion

	#region Test Helpers

	private sealed class TestDynamoDbRepository : DynamoDbRepositoryBase<SampleDocument>
	{
		public TestDynamoDbRepository(IAmazonDynamoDB client, string tableName, string partitionKeyName)
			: base(client, tableName, partitionKeyName)
		{
		}

		public string ExposedTableName => TableName;
		public string ExposedPartitionKeyName => PartitionKeyName;

		public override Task InitializeTableAsync(CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class SampleDocument
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	#endregion
}
