// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.DynamoDb.Authorization;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for the GrantItem class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify grant item constants and key creation.
/// Note: GrantItem is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Authorization")]
public sealed class GrantItemShould
{
	private readonly Type _itemType;

	public GrantItemShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(DynamoDbAuthorizationOptions).Assembly;
		_itemType = assembly.GetType("Excalibur.Data.DynamoDb.Authorization.GrantItem")!;
	}

	#region Constant Value Tests

	[Fact]
	public void NullTenantPartitionKey_Equals__null__()
	{
		// Arrange
		var field = _itemType.GetField("NullTenantPartitionKey", BindingFlags.NonPublic | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("__null__");
	}

	[Fact]
	public void PartitionKeyAttribute_Equals_tenant_id()
	{
		// Arrange
		var property = _itemType.GetProperty("PartitionKeyAttribute", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)property!.GetValue(null)!;

		// Assert
		value.ShouldBe("tenant_id");
	}

	[Fact]
	public void SortKeyAttribute_Equals_sk()
	{
		// Arrange
		var property = _itemType.GetProperty("SortKeyAttribute", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)property!.GetValue(null)!;

		// Assert
		value.ShouldBe("sk");
	}

	[Fact]
	public void GsiUserIdAttribute_Equals_gsi_user_id()
	{
		// Arrange
		var property = _itemType.GetProperty("GsiUserIdAttribute", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)property!.GetValue(null)!;

		// Assert
		value.ShouldBe("gsi_user_id");
	}

	[Fact]
	public void GsiSortKeyAttribute_Equals_gsi_sk()
	{
		// Arrange
		var property = _itemType.GetProperty("GsiSortKeyAttribute", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)property!.GetValue(null)!;

		// Assert
		value.ShouldBe("gsi_sk");
	}

	[Fact]
	public void IsRevokedAttribute_Equals_is_revoked()
	{
		// Arrange
		var property = _itemType.GetProperty("IsRevokedAttribute", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)property!.GetValue(null)!;

		// Assert
		value.ShouldBe("is_revoked");
	}

	[Fact]
	public void GrantTypeAttribute_Equals_grant_type()
	{
		// Arrange
		var property = _itemType.GetProperty("GrantTypeAttribute", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)property!.GetValue(null)!;

		// Assert
		value.ShouldBe("grant_type");
	}

	[Fact]
	public void QualifierAttribute_Equals_qualifier()
	{
		// Arrange
		var property = _itemType.GetProperty("QualifierAttribute", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)property!.GetValue(null)!;

		// Assert
		value.ShouldBe("qualifier");
	}

	[Fact]
	public void UserIdAttribute_Equals_user_id()
	{
		// Arrange
		var property = _itemType.GetProperty("UserIdAttribute", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)property!.GetValue(null)!;

		// Assert
		value.ShouldBe("user_id");
	}

	#endregion

	#region CreatePK Tests

	[Fact]
	public void CreatePK_ReturnsTenantId_WhenProvided()
	{
		// Arrange
		var method = _itemType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object?[] { "tenant-123" })!;

		// Assert
		result.ShouldBe("tenant-123");
	}

	[Fact]
	public void CreatePK_ReturnsNullTenantPartitionKey_WhenNull()
	{
		// Arrange
		var method = _itemType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object?[] { null })!;

		// Assert
		result.ShouldBe("__null__");
	}

	#endregion

	#region CreateSK Tests

	[Fact]
	public void CreateSK_ReturnsCorrectSortKey()
	{
		// Arrange
		var method = _itemType.GetMethod("CreateSK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { "user-123", "Role", "Admin" })!;

		// Assert
		result.ShouldBe("GRANT#user-123#Role#Admin");
	}

	#endregion

	#region CreateGsiSK Tests

	[Fact]
	public void CreateGsiSK_ReturnsCorrectKey_WithTenantId()
	{
		// Arrange
		var method = _itemType.GetMethod("CreateGsiSK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object?[] { "tenant-123", "Role", "Admin" })!;

		// Assert
		result.ShouldBe("tenant-123#GRANT#Role#Admin");
	}

	[Fact]
	public void CreateGsiSK_ReturnsCorrectKey_WithNullTenantId()
	{
		// Arrange
		var method = _itemType.GetMethod("CreateGsiSK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object?[] { null, "Role", "Admin" })!;

		// Assert
		result.ShouldBe("null#GRANT#Role#Admin");
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		_itemType.IsAbstract.ShouldBeTrue();
		_itemType.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsInternal()
	{
		// Assert
		_itemType.IsNotPublic.ShouldBeTrue();
	}

	#endregion
}
