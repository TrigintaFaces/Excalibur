// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="SchemaRegistryException"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify constructors and custom properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class SchemaRegistryExceptionShould
{
	#region Constructor Tests

	[Fact]
	public void DefaultConstructor_CreatesException()
	{
		// Act
		var exception = new SchemaRegistryException();

		// Assert
		exception.Message.ShouldNotBeEmpty();
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Arrange
		const string message = "Schema not found";

		// Act
		var exception = new SchemaRegistryException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void MessageAndInnerExceptionConstructor_SetsBoth()
	{
		// Arrange
		const string message = "Schema registry error";
		var inner = new InvalidOperationException("Connection failed");

		// Act
		var exception = new SchemaRegistryException(message, inner);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void Subject_DefaultsToNull()
	{
		// Act
		var exception = new SchemaRegistryException();

		// Assert
		exception.Subject.ShouldBeNull();
	}

	[Fact]
	public void Subject_CanBeSet()
	{
		// Arrange
		const string subject = "orders-value";

		// Act
		var exception = new SchemaRegistryException("Error") { Subject = subject };

		// Assert
		exception.Subject.ShouldBe(subject);
	}

	[Fact]
	public void SchemaId_DefaultsToNull()
	{
		// Act
		var exception = new SchemaRegistryException();

		// Assert
		exception.SchemaId.ShouldBeNull();
	}

	[Fact]
	public void SchemaId_CanBeSet()
	{
		// Arrange
		const int schemaId = 12345;

		// Act
		var exception = new SchemaRegistryException("Error") { SchemaId = schemaId };

		// Assert
		exception.SchemaId.ShouldBe(schemaId);
	}

	[Fact]
	public void StatusCode_DefaultsToNull()
	{
		// Act
		var exception = new SchemaRegistryException();

		// Assert
		exception.StatusCode.ShouldBeNull();
	}

	[Fact]
	public void StatusCode_CanBeSet()
	{
		// Arrange
		const int statusCode = 404;

		// Act
		var exception = new SchemaRegistryException("Not found") { StatusCode = statusCode };

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
	}

	[Fact]
	public void ErrorCode_DefaultsToNull()
	{
		// Act
		var exception = new SchemaRegistryException();

		// Assert
		exception.ErrorCode.ShouldBeNull();
	}

	[Fact]
	public void ErrorCode_CanBeSet()
	{
		// Arrange
		const int errorCode = 40401;

		// Act
		var exception = new SchemaRegistryException("Error") { ErrorCode = errorCode };

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
	}

	#endregion

	#region Full Exception Tests

	[Fact]
	public void AllProperties_CanBeSetTogether()
	{
		// Arrange
		var inner = new HttpRequestException("Connection refused");

		// Act
		var exception = new SchemaRegistryException("Failed to register schema", inner)
		{
			Subject = "events-value",
			SchemaId = 999,
			StatusCode = 500,
			ErrorCode = 50001
		};

		// Assert
		exception.Message.ShouldBe("Failed to register schema");
		exception.InnerException.ShouldBe(inner);
		exception.Subject.ShouldBe("events-value");
		exception.SchemaId.ShouldBe(999);
		exception.StatusCode.ShouldBe(500);
		exception.ErrorCode.ShouldBe(50001);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromException()
	{
		// Assert
		typeof(SchemaRegistryException).IsSubclassOf(typeof(Exception)).ShouldBeTrue();
	}

	[Fact]
	public void HasSerializableAttribute()
	{
		// Assert
		typeof(SchemaRegistryException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void CanBeCaughtAsException()
	{
		// Arrange
		var exception = new SchemaRegistryException("Test");

		// Act & Assert
		try
		{
			throw exception;
		}
		catch (Exception ex)
		{
			ex.ShouldBeOfType<SchemaRegistryException>();
		}
	}

	#endregion
}
