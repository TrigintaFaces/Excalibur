// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.DynamoDb.Snapshots;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for the DynamoDbSnapshotDocument class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify snapshot document constants and key creation.
/// Note: DynamoDbSnapshotDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Snapshots")]
public sealed class DynamoDbSnapshotDocumentShould
{
	private readonly Type _documentType;

	public DynamoDbSnapshotDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(DynamoDbSnapshotStoreOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Data.DynamoDb.Snapshots.DynamoDbSnapshotDocument")!;
	}

	#region Constant Value Tests

	[Fact]
	public void PK_Constant_Equals_PK()
	{
		// Arrange
		var field = _documentType.GetField("PK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("PK");
	}

	[Fact]
	public void SK_Constant_Equals_SK()
	{
		// Arrange
		var field = _documentType.GetField("SK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("SK");
	}

	[Fact]
	public void SnapshotPrefix_Constant_Equals_SNAPSHOT_Hash()
	{
		// Arrange
		var field = _documentType.GetField("SnapshotPrefix", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("SNAPSHOT#");
	}

	[Fact]
	public void SnapshotId_Constant_Equals_snapshotId()
	{
		// Arrange
		var field = _documentType.GetField("SnapshotId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("snapshotId");
	}

	[Fact]
	public void Version_Constant_Equals_version()
	{
		// Arrange
		var field = _documentType.GetField("Version", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("version");
	}

	[Fact]
	public void AggregateId_Constant_Equals_aggregateId()
	{
		// Arrange
		var field = _documentType.GetField("AggregateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("aggregateId");
	}

	[Fact]
	public void AggregateType_Constant_Equals_aggregateType()
	{
		// Arrange
		var field = _documentType.GetField("AggregateType", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("aggregateType");
	}

	[Fact]
	public void Data_Constant_Equals_data()
	{
		// Arrange
		var field = _documentType.GetField("Data", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("data");
	}

	[Fact]
	public void Metadata_Constant_Equals_metadata()
	{
		// Arrange
		var field = _documentType.GetField("Metadata", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("metadata");
	}

	[Fact]
	public void CreatedAt_Constant_Equals_createdAt()
	{
		// Arrange
		var field = _documentType.GetField("CreatedAt", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("createdAt");
	}

	[Fact]
	public void Ttl_Constant_Equals_ttl()
	{
		// Arrange
		var field = _documentType.GetField("Ttl", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("ttl");
	}

	#endregion

	#region CreatePK Tests

	[Fact]
	public void CreatePK_ReturnsCorrectPartitionKey()
	{
		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { "aggregate-123" })!;

		// Assert
		result.ShouldBe("SNAPSHOT#aggregate-123");
	}

	[Fact]
	public void CreatePK_PreservesSpecialCharacters()
	{
		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);

		// Act - DynamoDB allows special characters in keys unlike CosmosDB
		var result = (string)method!.Invoke(null, new object[] { "aggregate/with/slashes" })!;

		// Assert
		result.ShouldBe("SNAPSHOT#aggregate/with/slashes");
	}

	#endregion

	#region CreateSK Tests

	[Fact]
	public void CreateSK_ReturnsAggregateType()
	{
		// Arrange
		var method = _documentType.GetMethod("CreateSK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { "OrderAggregate" })!;

		// Assert
		result.ShouldBe("OrderAggregate");
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		_documentType.IsAbstract.ShouldBeTrue();
		_documentType.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsInternal()
	{
		// Assert
		_documentType.IsNotPublic.ShouldBeTrue();
	}

	#endregion
}
