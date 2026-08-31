// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Saga.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for the DynamoDbSagaDocument class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify saga document constants and key creation.
/// Note: DynamoDbSagaDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Saga")]
public sealed class DynamoDbSagaDocumentShould
{
	private readonly Type _documentType;

	public DynamoDbSagaDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(DynamoDbSagaOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Saga.DynamoDb.DynamoDbSagaDocument")!;
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
	public void SagaPrefix_Constant_Equals_SAGA_Hash()
	{
		// Arrange
		var field = _documentType.GetField("SagaPrefix", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("SAGA#");
	}

	[Fact]
	public void SagaId_Constant_Equals_sagaId()
	{
		// Arrange
		var field = _documentType.GetField("SagaId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("sagaId");
	}

	[Fact]
	public void SagaType_Constant_Equals_sagaType()
	{
		// Arrange
		var field = _documentType.GetField("SagaType", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("sagaType");
	}

	[Fact]
	public void StateJson_Constant_Equals_stateJson()
	{
		// Arrange
		var field = _documentType.GetField("StateJson", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("stateJson");
	}

	[Fact]
	public void IsCompleted_Constant_Equals_isCompleted()
	{
		// Arrange
		var field = _documentType.GetField("IsCompleted", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("isCompleted");
	}

	[Fact]
	public void CreatedUtc_Constant_Equals_createdUtc()
	{
		// Arrange
		var field = _documentType.GetField("CreatedUtc", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("createdUtc");
	}

	[Fact]
	public void UpdatedUtc_Constant_Equals_updatedUtc()
	{
		// Arrange
		var field = _documentType.GetField("UpdatedUtc", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("updatedUtc");
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
	public void CreatePK_ComposesTheOwningTenantWithTheSagaId()
	{
		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);
		var sagaId = new Guid("12345678-1234-1234-1234-123456789abc");

		// Act
		var result = (string)method!.Invoke(null, new object[] { "tenant-a", sagaId })!;

		// Assert
		result.ShouldBe("SAGA#t:tenant-a:12345678-1234-1234-1234-123456789abc");
	}

	[Fact]
	public void CreatePK_GivesTwoTenantsDistinctPartitionKeys_ForTheSameSagaId()
	{
		// The saga identifier is a business correlation key, so two tenants legitimately run a saga at the
		// same one. If the partition key did not carry the tenant they would address ONE item, and the
		// tenant term in the write condition -- which correctly refuses a cross-tenant overwrite -- would
		// also refuse the second tenant its own saga.

		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);
		var sagaId = Guid.NewGuid();

		// Act
		var pkForA = (string)method!.Invoke(null, new object[] { "tenant-a", sagaId })!;
		var pkForB = (string)method!.Invoke(null, new object[] { "tenant-b", sagaId })!;

		// Assert
		pkForA.ShouldNotBe(pkForB);
	}

	[Fact]
	public void CreatePK_CarriesTheUntenantedSentinel_SoNoKeyIsProducedWithoutATenantSegment()
	{
		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);
		var sagaId = new Guid("12345678-1234-1234-1234-123456789abc");

		// Act
		var result = (string)method!.Invoke(null, new object[] { TenantScope.Untenanted.TenantId, sagaId })!;

		// Assert
		result.ShouldBe($"SAGA#t:{TenantScope.Untenanted.TenantId}:12345678-1234-1234-1234-123456789abc");
	}

	#endregion

	#region CreateSK Tests

	[Fact]
	public void CreateSK_ReturnsSagaType()
	{
		// Arrange
		var method = _documentType.GetMethod("CreateSK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { "OrderSagaState" })!;

		// Assert
		result.ShouldBe("OrderSagaState");
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
