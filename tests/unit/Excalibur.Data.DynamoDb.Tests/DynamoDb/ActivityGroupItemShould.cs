// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.DynamoDb.Authorization;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for the ActivityGroupItem class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify activity group item constants and key creation.
/// Note: ActivityGroupItem is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Authorization")]
public sealed class ActivityGroupItemShould
{
	private readonly Type _itemType;

	public ActivityGroupItemShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(DynamoDbAuthorizationOptions).Assembly;
		_itemType = assembly.GetType("Excalibur.Data.DynamoDb.Authorization.ActivityGroupItem")!;
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
		var result = (string)method!.Invoke(null, new object?[] { "tenant-456" })!;

		// Assert
		result.ShouldBe("tenant-456");
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
		var result = (string)method!.Invoke(null, new object[] { "user-456", "Group", "Developers" })!;

		// Assert
		result.ShouldBe("ACTGRP#user-456#Group#Developers");
	}

	#endregion

	#region CreateGsiSK Tests

	[Fact]
	public void CreateGsiSK_ReturnsCorrectKey_WithTenantId()
	{
		// Arrange
		var method = _itemType.GetMethod("CreateGsiSK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object?[] { "tenant-456", "Group", "Developers" })!;

		// Assert
		result.ShouldBe("tenant-456#ACTGRP#Group#Developers");
	}

	[Fact]
	public void CreateGsiSK_ReturnsCorrectKey_WithNullTenantId()
	{
		// Arrange
		var method = _itemType.GetMethod("CreateGsiSK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object?[] { null, "Group", "Developers" })!;

		// Assert
		result.ShouldBe("null#ACTGRP#Group#Developers");
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
