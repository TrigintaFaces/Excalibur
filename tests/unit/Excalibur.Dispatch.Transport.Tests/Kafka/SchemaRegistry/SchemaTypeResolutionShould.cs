// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="SchemaTypeResolution"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify factory methods and property behavior for type resolution results.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class SchemaTypeResolutionShould
{
	#region Success Factory Tests

	[Fact]
	public void Success_CreatesSuccessfulResolution()
	{
		// Arrange
		const int schemaId = 123;
		var messageType = typeof(TestMessage);
		const string messageTypeName = "TestMessage";
		const string schemaJson = """{"type":"object"}""";

		// Act
		var result = SchemaTypeResolution.Success(schemaId, messageType, messageTypeName, schemaJson);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.SchemaId.ShouldBe(schemaId);
		result.MessageType.ShouldBe(messageType);
		result.MessageTypeName.ShouldBe(messageTypeName);
		result.SchemaJson.ShouldBe(schemaJson);
		result.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void Success_ThrowsForNullMessageType()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SchemaTypeResolution.Success(1, null!, "Name", "{}"));
	}

	[Fact]
	public void Success_ThrowsForNullMessageTypeName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SchemaTypeResolution.Success(1, typeof(object), null!, "{}"));
	}

	[Fact]
	public void Success_ThrowsForNullSchemaJson()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SchemaTypeResolution.Success(1, typeof(object), "Name", null!));
	}

	#endregion

	#region Failed Factory Tests

	[Fact]
	public void Failed_CreatesFailedResolution()
	{
		// Arrange
		const int schemaId = 456;
		const string schemaJson = """{"type":"string"}""";
		const string reason = "Type not found";

		// Act
		var result = SchemaTypeResolution.Failed(schemaId, schemaJson, reason);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.SchemaId.ShouldBe(schemaId);
		result.MessageType.ShouldBe(typeof(object));
		result.MessageTypeName.ShouldBe(string.Empty);
		result.SchemaJson.ShouldBe(schemaJson);
		result.FailureReason.ShouldBe(reason);
	}

	[Fact]
	public void Failed_HandlesNullSchemaJson()
	{
		// Act
		var result = SchemaTypeResolution.Failed(789, null, "Schema unavailable");

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.SchemaJson.ShouldBe(string.Empty);
		result.FailureReason.ShouldBe("Schema unavailable");
	}

	[Fact]
	public void Failed_UsesObjectAsDefaultMessageType()
	{
		// Act
		var result = SchemaTypeResolution.Failed(100, "{}", "Unknown type");

		// Assert
		result.MessageType.ShouldBe(typeof(object));
	}

	[Fact]
	public void Failed_UsesEmptyStringAsDefaultMessageTypeName()
	{
		// Act
		var result = SchemaTypeResolution.Failed(100, "{}", "Unknown type");

		// Assert
		result.MessageTypeName.ShouldBe(string.Empty);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void SchemaId_IsSet()
	{
		// Arrange
		const int expectedId = 42;

		// Act
		var result = SchemaTypeResolution.Success(expectedId, typeof(string), "String", "{}");

		// Assert
		result.SchemaId.ShouldBe(expectedId);
	}

	[Fact]
	public void MessageType_CanBeAnyType()
	{
		// Arrange
		var types = new[] { typeof(string), typeof(int), typeof(TestMessage), typeof(List<string>) };

		foreach (var type in types)
		{
			// Act
			var result = SchemaTypeResolution.Success(1, type, type.Name, "{}");

			// Assert
			result.MessageType.ShouldBe(type);
		}
	}

	[Fact]
	public void SchemaJson_PreservesFormatting()
	{
		// Arrange
		const string formattedJson = """
			{
			  "type": "object",
			  "properties": {
			    "id": {"type": "integer"}
			  }
			}
			""";

		// Act
		var result = SchemaTypeResolution.Success(1, typeof(object), "Obj", formattedJson);

		// Assert
		result.SchemaJson.ShouldBe(formattedJson);
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void Resolution_IsImmutable()
	{
		// Arrange
		var resolution = SchemaTypeResolution.Success(1, typeof(TestMessage), "Test", "{}");

		// Assert - All properties are getters only (no setters)
		resolution.GetType().GetProperty(nameof(SchemaTypeResolution.SchemaId)).CanWrite.ShouldBeFalse();
		resolution.GetType().GetProperty(nameof(SchemaTypeResolution.MessageType)).CanWrite.ShouldBeFalse();
		resolution.GetType().GetProperty(nameof(SchemaTypeResolution.MessageTypeName)).CanWrite.ShouldBeFalse();
		resolution.GetType().GetProperty(nameof(SchemaTypeResolution.SchemaJson)).CanWrite.ShouldBeFalse();
		resolution.GetType().GetProperty(nameof(SchemaTypeResolution.IsSuccess)).CanWrite.ShouldBeFalse();
		resolution.GetType().GetProperty(nameof(SchemaTypeResolution.FailureReason)).CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(SchemaTypeResolution).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Test Helpers

	private sealed class TestMessage
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}

	#endregion
}
