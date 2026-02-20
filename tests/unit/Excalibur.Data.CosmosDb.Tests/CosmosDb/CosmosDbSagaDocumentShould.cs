// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json.Serialization;

using Excalibur.Data.CosmosDb.Saga;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbSagaDocument"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify document properties and the CreateId factory method.
/// Note: CosmosDbSagaDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Saga")]
public sealed class CosmosDbSagaDocumentShould
{
	private readonly Type _documentType;

	public CosmosDbSagaDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(CosmosDbSagaOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Data.CosmosDb.Saga.CosmosDbSagaDocument")!;
	}

	#region CreateId Tests

	[Fact]
	public void CreateId_ReturnsGuidAsString()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = createIdMethod.Invoke(null, new object[] { sagaId });

		// Assert
		result.ShouldBe(sagaId.ToString());
	}

	[Fact]
	public void CreateId_ReturnsEmptyGuidString_ForEmptyGuid()
	{
		// Arrange
		var sagaId = Guid.Empty;
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = createIdMethod.Invoke(null, new object[] { sagaId });

		// Assert
		result.ShouldBe(Guid.Empty.ToString());
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
	public void SagaId_DefaultsToEmptyGuid()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var sagaIdProperty = _documentType.GetProperty("SagaId");

		// Assert
		sagaIdProperty.GetValue(document).ShouldBe(Guid.Empty);
	}

	[Fact]
	public void SagaType_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var sagaTypeProperty = _documentType.GetProperty("SagaType");

		// Assert
		sagaTypeProperty.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void StateJson_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var stateJsonProperty = _documentType.GetProperty("StateJson");

		// Assert
		stateJsonProperty.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void IsCompleted_DefaultsToFalse()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var isCompletedProperty = _documentType.GetProperty("IsCompleted");

		// Assert
		isCompletedProperty.GetValue(document).ShouldBe(false);
	}

	#endregion

	#region JsonPropertyName Attribute Tests

	[Fact]
	public void Id_HasCorrectJsonPropertyName()
	{
		// Arrange
		var idProperty = _documentType.GetProperty("Id");
		var attribute = idProperty.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("id");
	}

	[Fact]
	public void SagaId_HasCorrectJsonPropertyName()
	{
		// Arrange
		var sagaIdProperty = _documentType.GetProperty("SagaId");
		var attribute = sagaIdProperty.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("sagaId");
	}

	[Fact]
	public void SagaType_HasCorrectJsonPropertyName()
	{
		// Arrange
		var sagaTypeProperty = _documentType.GetProperty("SagaType");
		var attribute = sagaTypeProperty.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("sagaType");
	}

	[Fact]
	public void StateJson_HasCorrectJsonPropertyName()
	{
		// Arrange
		var stateJsonProperty = _documentType.GetProperty("StateJson");
		var attribute = stateJsonProperty.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("stateJson");
	}

	[Fact]
	public void IsCompleted_HasCorrectJsonPropertyName()
	{
		// Arrange
		var isCompletedProperty = _documentType.GetProperty("IsCompleted");
		var attribute = isCompletedProperty.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("isCompleted");
	}

	[Fact]
	public void CreatedUtc_HasCorrectJsonPropertyName()
	{
		// Arrange
		var createdUtcProperty = _documentType.GetProperty("CreatedUtc");
		var attribute = createdUtcProperty.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("createdUtc");
	}

	[Fact]
	public void UpdatedUtc_HasCorrectJsonPropertyName()
	{
		// Arrange
		var updatedUtcProperty = _documentType.GetProperty("UpdatedUtc");
		var attribute = updatedUtcProperty.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("updatedUtc");
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

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeSet()
	{
		// Arrange
		var document = Activator.CreateInstance(_documentType);
		var sagaId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		// Act
		_documentType.GetProperty("Id").SetValue(document, "test-id");
		_documentType.GetProperty("SagaId").SetValue(document, sagaId);
		_documentType.GetProperty("SagaType").SetValue(document, "OrderSaga");
		_documentType.GetProperty("StateJson").SetValue(document, "{\"order\":123}");
		_documentType.GetProperty("IsCompleted").SetValue(document, true);
		_documentType.GetProperty("CreatedUtc").SetValue(document, now);
		_documentType.GetProperty("UpdatedUtc").SetValue(document, now);

		// Assert
		_documentType.GetProperty("Id").GetValue(document).ShouldBe("test-id");
		_documentType.GetProperty("SagaId").GetValue(document).ShouldBe(sagaId);
		_documentType.GetProperty("SagaType").GetValue(document).ShouldBe("OrderSaga");
		_documentType.GetProperty("StateJson").GetValue(document).ShouldBe("{\"order\":123}");
		_documentType.GetProperty("IsCompleted").GetValue(document).ShouldBe(true);
		_documentType.GetProperty("CreatedUtc").GetValue(document).ShouldBe(now);
		_documentType.GetProperty("UpdatedUtc").GetValue(document).ShouldBe(now);
	}

	#endregion
}
