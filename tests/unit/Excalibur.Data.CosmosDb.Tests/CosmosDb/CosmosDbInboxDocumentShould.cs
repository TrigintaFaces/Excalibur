// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json.Serialization;

using Excalibur.Data.CosmosDb.Inbox;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbInboxDocument"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify document properties and the CreateId factory method.
/// Note: CosmosDbInboxDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Inbox")]
public sealed class CosmosDbInboxDocumentShould
{
	private readonly Type _documentType;

	public CosmosDbInboxDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(CosmosDbInboxOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Data.CosmosDb.Inbox.CosmosDbInboxDocument")!;
	}

	#region CreateId Tests

	[Fact]
	public void CreateId_ReturnsCompositeId()
	{
		// Arrange
		var messageId = "msg-123";
		var handlerType = "OrderHandler";
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object[] { messageId, handlerType })!;

		// Assert
		result.ShouldBe("msg-123:OrderHandler");
	}

	[Fact]
	public void CreateId_HandlesEmptyStrings()
	{
		// Arrange
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object[] { "", "" })!;

		// Assert
		result.ShouldBe(":");
	}

	[Fact]
	public void CreateId_PreservesColonInValues()
	{
		// Arrange
		var messageId = "msg:123";
		var handlerType = "Order:Handler";
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object[] { messageId, handlerType })!;

		// Assert
		result.ShouldBe("msg:123:Order:Handler");
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
	public void MessageId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("MessageId");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void HandlerType_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("HandlerType");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void MessageType_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("MessageType");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Payload_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Payload");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Metadata_DefaultsToEmptyDictionary()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType)!;
		var property = _documentType.GetProperty("Metadata");
		var metadata = (IDictionary<string, object>)property!.GetValue(document)!;

		// Assert
		metadata.ShouldNotBeNull();
		metadata.Count.ShouldBe(0);
	}

	[Fact]
	public void Status_DefaultsToZero()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Status");

		// Assert
		property.GetValue(document).ShouldBe(0);
	}

	[Fact]
	public void RetryCount_DefaultsToZero()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("RetryCount");

		// Assert
		property.GetValue(document).ShouldBe(0);
	}

	[Fact]
	public void LastError_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("LastError");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void ProcessedAt_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("ProcessedAt");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void LastAttemptAt_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("LastAttemptAt");

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
	public void MessageId_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("MessageId");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("message_id");
	}

	[Fact]
	public void HandlerType_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("HandlerType");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("handler_type");
	}

	[Fact]
	public void Status_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Status");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("status");
	}

	[Fact]
	public void RetryCount_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("RetryCount");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("retry_count");
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
