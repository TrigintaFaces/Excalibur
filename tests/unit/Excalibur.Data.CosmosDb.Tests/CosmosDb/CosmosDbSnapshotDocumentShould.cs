// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json.Serialization;

using Excalibur.Data.CosmosDb.Snapshots;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbSnapshotDocument"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify document properties and the CreateId factory method.
/// Note: CosmosDbSnapshotDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Snapshots")]
public sealed class CosmosDbSnapshotDocumentShould
{
	private readonly Type _documentType;

	public CosmosDbSnapshotDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(CosmosDbSnapshotStoreOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Data.CosmosDb.Snapshots.CosmosDbSnapshotDocument")!;
	}

	#region CreateId Tests

	[Fact]
	public void CreateId_ReturnsUrlSafeBase64()
	{
		// Arrange
		var aggregateId = "test-aggregate-123";
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object[] { aggregateId })!;

		// Assert
		result.ShouldNotContain("+");
		result.ShouldNotContain("/");
		result.ShouldNotContain("=");
	}

	[Fact]
	public void CreateId_HandlesSpecialCharacters()
	{
		// Arrange - Characters that need escaping: / \ ? #
		var aggregateId = "order/123\\test?query#fragment";
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object[] { aggregateId })!;

		// Assert - Should be URL-safe Base64 (no + / =)
		result.ShouldNotContain("+");
		result.ShouldNotContain("/");
		result.ShouldNotContain("=");
	}

	[Fact]
	public void CreateId_ProducesConsistentResults()
	{
		// Arrange
		var aggregateId = "my-aggregate-id";
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result1 = (string)createIdMethod!.Invoke(null, new object[] { aggregateId })!;
		var result2 = (string)createIdMethod.Invoke(null, new object[] { aggregateId })!;

		// Assert
		result1.ShouldBe(result2);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void Id_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var idProperty = _documentType.GetProperty("Id");

		// Assert
		idProperty.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void AggregateId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("AggregateId");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void AggregateType_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("AggregateType");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void SnapshotId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("SnapshotId");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Version_DefaultsToZero()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Version");

		// Assert
		property.GetValue(document).ShouldBe(0L);
	}

	[Fact]
	public void Data_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Data");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Metadata_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Metadata");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void CreatedAt_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("CreatedAt");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Ttl_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Ttl");

		// Assert
		property.GetValue(document).ShouldBeNull();
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
	public void AggregateId_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("AggregateId");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("aggregateId");
	}

	[Fact]
	public void AggregateType_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("AggregateType");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("aggregateType");
	}

	[Fact]
	public void Version_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Version");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("version");
	}

	[Fact]
	public void Data_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Data");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("data");
	}

	[Fact]
	public void Ttl_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Ttl");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("ttl");
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
