// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json.Serialization;

using Excalibur.Data.CosmosDb.Outbox;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbOutboxDocument"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify document properties and the CreatePartitionKey factory method.
/// Note: CosmosDbOutboxDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Outbox")]
public sealed class CosmosDbOutboxDocumentShould
{
	private readonly Type _documentType;

	public CosmosDbOutboxDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(CosmosDbOutboxOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Data.CosmosDb.Outbox.CosmosDbOutboxDocument")!;
	}

	#region CreatePartitionKey Tests

	[Theory]
	[InlineData(OutboxStatus.Staged, "Staged")]
	[InlineData(OutboxStatus.Sending, "Sending")]
	[InlineData(OutboxStatus.Sent, "Sent")]
	[InlineData(OutboxStatus.Failed, "Failed")]
	[InlineData(OutboxStatus.PartiallyFailed, "PartiallyFailed")]
	public void CreatePartitionKey_ReturnsStatusAsString(OutboxStatus status, string expected)
	{
		// Arrange
		var createPartitionKeyMethod = _documentType.GetMethod("CreatePartitionKey", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createPartitionKeyMethod!.Invoke(null, new object[] { status })!;

		// Assert
		result.ShouldBe(expected);
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
	public void PartitionKey_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("PartitionKey");

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
	public void Destination_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Destination");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Headers_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Headers");

		// Assert
		property.GetValue(document).ShouldBeNull();
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
	public void Priority_DefaultsToZero()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Priority");

		// Assert
		property.GetValue(document).ShouldBe(0);
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
	public void CorrelationId_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("CorrelationId");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void CausationId_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("CausationId");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void TenantId_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("TenantId");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void ETag_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("ETag");

		// Assert
		property.GetValue(document).ShouldBeNull();
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
	public void PartitionKey_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("PartitionKey");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("partitionKey");
	}

	[Fact]
	public void MessageType_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("MessageType");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("messageType");
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
	public void ETag_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("ETag");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("_etag");
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
