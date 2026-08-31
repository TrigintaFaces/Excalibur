// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.DynamoDb.Snapshots;
using Excalibur.Dispatch;

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

	// CreatePK takes the tenant partition as a REQUIRED argument. The tenant-less single-argument
	// overload is gone: the store always resolves the partition from the total tenant term, so it never
	// produced the tenant-less shape, and a factory that could still emit one only offered a key no read
	// path composes.
	private string CreatePK(string aggregateId, string tenantPartition)
	{
		var method = _documentType.GetMethod(
			"CreatePK", BindingFlags.Public | BindingFlags.Static, binder: null,
			types: [typeof(string), typeof(string)], modifiers: null);

		method.ShouldNotBeNull("CreatePK(aggregateId, tenantPartition) must exist.");
		return (string)method.Invoke(null, [aggregateId, tenantPartition])!;
	}

	[Fact]
	public void CreatePK_HasNoTenantLessOverload()
	{
		// The invariant this file exists to hold: one key shape per item. A second, tenant-omitting form
		// is what lets a read and a write disagree about which of two shapes to address.
		_documentType.GetMethod(
				"CreatePK", BindingFlags.Public | BindingFlags.Static, binder: null,
				types: [typeof(string)], modifiers: null)
			.ShouldBeNull("A tenant-less CreatePK would emit a partition key no read path composes.");

		// Positive control: the reflection query itself discriminates, so the null above is a real
		// absence rather than a lookup that could never have matched.
		_documentType.GetMethod(
				"CreatePK", BindingFlags.Public | BindingFlags.Static, binder: null,
				types: [typeof(string), typeof(string)], modifiers: null)
			.ShouldNotBeNull();
	}

	[Fact]
	public void CreatePK_CarriesTheTenantSegment_ForEveryPartition()
	{
		// LIVENESS: a real tenant and the untenanted partition both yield a well-formed, prefixed key.
		// Without this arm a builder that returned the empty string for everything would satisfy the
		// safety arm below perfectly.
		CreatePK("aggregate-123", "tenant-a").ShouldBe("SNAPSHOT#t:tenant-a:aggregate-123");
		CreatePK("aggregate-123", TenantScope.UntenantedSentinel)
			.ShouldBe($"SNAPSHOT#t:{TenantScope.UntenantedSentinel}:aggregate-123");
	}

	[Fact]
	public void CreatePK_SeparatesTenants_AndSeparatesUntenantedFromTenanted()
	{
		// SAFETY: no two partitions share a key for the same aggregate. The untenanted partition is a
		// value like any other, so it is separated too - it is not an absence.
		const string AggregateId = "aggregate-123";

		var tenantA = CreatePK(AggregateId, "tenant-a");
		var tenantB = CreatePK(AggregateId, "tenant-b");
		var untenanted = CreatePK(AggregateId, TenantScope.UntenantedSentinel);

		tenantA.ShouldNotBe(tenantB);
		tenantA.ShouldNotBe(untenanted);
		tenantB.ShouldNotBe(untenanted);
	}

	[Fact]
	public void CreatePK_PreservesSpecialCharacters()
	{
		// DynamoDB allows special characters in keys, unlike CosmosDB.
		CreatePK("aggregate/with/slashes", "tenant-a")
			.ShouldBe("SNAPSHOT#t:tenant-a:aggregate/with/slashes");
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
