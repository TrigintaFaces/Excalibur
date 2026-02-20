// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json.Serialization;

using Excalibur.Data.CosmosDb.Authorization;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the ActivityGroupDocument class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify document properties and factory methods.
/// Note: ActivityGroupDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Authorization")]
public sealed class ActivityGroupDocumentShould
{
	private readonly Type _documentType;

	public ActivityGroupDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(CosmosDbAuthorizationOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Data.CosmosDb.Authorization.ActivityGroupDocument")!;
	}

	#region NullTenantPartitionKey Tests

	[Fact]
	public void NullTenantPartitionKey_HasCorrectValue()
	{
		// Arrange
		var field = _documentType.GetField("NullTenantPartitionKey", BindingFlags.Static | BindingFlags.NonPublic);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("__null__");
	}

	#endregion

	#region CreateId Tests

	[Fact]
	public void CreateId_ReturnsCompositeId()
	{
		// Arrange
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object?[] { "user-1", "tenant-1", "activity-group", "editors" })!;

		// Assert
		result.ShouldBe("user-1:tenant-1:activity-group:editors");
	}

	[Fact]
	public void CreateId_HandlesNullTenant()
	{
		// Arrange
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object?[] { "user-1", null, "activity-group", "editors" })!;

		// Assert
		result.ShouldBe("user-1:null:activity-group:editors");
	}

	#endregion

	#region GetPartitionKey Tests

	[Fact]
	public void GetPartitionKey_ReturnsTenantId_WhenNotNull()
	{
		// Arrange
		var getPartitionKeyMethod = _documentType.GetMethod("GetPartitionKey", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)getPartitionKeyMethod!.Invoke(null, new object?[] { "tenant-1" })!;

		// Assert
		result.ShouldBe("tenant-1");
	}

	[Fact]
	public void GetPartitionKey_ReturnsNullTenantPartitionKey_WhenNull()
	{
		// Arrange
		var getPartitionKeyMethod = _documentType.GetMethod("GetPartitionKey", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)getPartitionKeyMethod!.Invoke(null, new object?[] { null })!;

		// Assert
		result.ShouldBe("__null__");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void Id_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Id");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void TenantId_DefaultsToNullTenantPartitionKey()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("TenantId");

		// Assert
		property.GetValue(document).ShouldBe("__null__");
	}

	[Fact]
	public void OriginalTenantId_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("OriginalTenantId");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void UserId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("UserId");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void FullName_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("FullName");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void GrantType_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("GrantType");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Qualifier_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Qualifier");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void ExpiresOn_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("ExpiresOn");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void GrantedBy_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("GrantedBy");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	#endregion

	#region JsonPropertyName Attribute Tests

	[Fact]
	public void Id_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Id");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("id");
	}

	[Fact]
	public void TenantId_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("TenantId");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("tenant_id");
	}

	[Fact]
	public void UserId_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("UserId");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("user_id");
	}

	[Fact]
	public void GrantType_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("GrantType");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("grant_type");
	}

	[Fact]
	public void CreatedAt_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("CreatedAt");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("created_at");
	}

	[Fact]
	public void UpdatedAt_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("UpdatedAt");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("updated_at");
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
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
