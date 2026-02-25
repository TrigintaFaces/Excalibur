// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="OperationFailedException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "Exceptions")]
public sealed class OperationFailedExceptionShould : UnitTestBase
{
	#region Constants Tests

	[Fact]
	public void Have_DefaultStatusCode_Of_500()
	{
		// Assert
		OperationFailedException.DefaultStatusCode.ShouldBe(500);
	}

	[Fact]
	public void Have_DefaultMessage_Describing_Operation_Failure()
	{
		// Assert
		OperationFailedException.DefaultMessage.ShouldBe("The operation failed.");
	}

	#endregion

	#region Constructor Tests (Operation, Resource)

	[Fact]
	public void Create_WithOperationAndResource_StoresOperation()
	{
		// Arrange
		var operation = "SaveAsync";
		var resource = "Customer";

		// Act
		var exception = new OperationFailedException(operation, resource);

		// Assert
		exception.Operation.ShouldBe(operation);
	}

	[Fact]
	public void Create_WithOperationAndResource_StoresResource()
	{
		// Arrange
		var operation = "SaveAsync";
		var resource = "Customer";

		// Act
		var exception = new OperationFailedException(operation, resource);

		// Assert
		exception.Resource.ShouldBe(resource);
	}

	[Fact]
	public void Create_WithOperationAndResource_UsesDefaultStatusCode()
	{
		// Arrange
		var operation = "SaveAsync";
		var resource = "Customer";

		// Act
		var exception = new OperationFailedException(operation, resource);

		// Assert
		exception.StatusCode.ShouldBe(OperationFailedException.DefaultStatusCode);
	}

	[Fact]
	public void Create_WithOperationAndResource_UsesDefaultMessage()
	{
		// Arrange
		var operation = "SaveAsync";
		var resource = "Customer";

		// Act
		var exception = new OperationFailedException(operation, resource);

		// Assert
		exception.Message.ShouldBe(OperationFailedException.DefaultMessage);
	}

	[Fact]
	public void Create_WithNullOperation_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OperationFailedException(null!, "Customer"));
	}

	[Fact]
	public void Create_WithNullResource_ThrowsArgumentNullException()
	{
		// Act & Assert - use named parameters to disambiguate
		Should.Throw<ArgumentNullException>(() =>
			new OperationFailedException(operation: "SaveAsync", resource: null!));
	}

	#endregion

	#region Constructor Tests (Custom Status Code)

	[Fact]
	public void Create_WithCustomStatusCode_StoresStatusCode()
	{
		// Arrange
		var operation = "GetAsync";
		var resource = "Order";
		var statusCode = 404;

		// Act - use named parameters to disambiguate
		var exception = new OperationFailedException(operation: operation, resource: resource, statusCode: statusCode);

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
	}

	[Theory]
	[InlineData(400)]
	[InlineData(401)]
	[InlineData(403)]
	[InlineData(404)]
	[InlineData(409)]
	[InlineData(422)]
	[InlineData(500)]
	[InlineData(503)]
	public void Create_WithVariousStatusCodes_StoresCorrectly(int statusCode)
	{
		// Arrange
		var operation = "TestOp";
		var resource = "TestResource";

		// Act - use named parameter to disambiguate
		var exception = new OperationFailedException(operation: operation, resource: resource, statusCode: statusCode);

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
	}

	#endregion

	#region Constructor Tests (Custom Message)

	[Fact]
	public void Create_WithCustomMessage_StoresMessage()
	{
		// Arrange
		var operation = "DeleteAsync";
		var resource = "User";
		var message = "Cannot delete user with active subscriptions";

		// Act
		var exception = new OperationFailedException(operation, resource, null, message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Create_WithNullMessage_UsesDefaultMessage()
	{
		// Arrange
		var operation = "UpdateAsync";
		var resource = "Product";

		// Act
		var exception = new OperationFailedException(operation, resource, null, null);

		// Assert
		exception.Message.ShouldBe(OperationFailedException.DefaultMessage);
	}

	#endregion

	#region Constructor Tests (Inner Exception)

	[Fact]
	public void Create_WithInnerException_StoresInnerException()
	{
		// Arrange
		var operation = "ConnectAsync";
		var resource = "Database";
		var innerException = new InvalidOperationException("Connection pool exhausted");

		// Act
		var exception = new OperationFailedException(operation, resource, null, null, innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void Create_WithAllParameters_StoresAllValues()
	{
		// Arrange
		var operation = "ExecuteAsync";
		var resource = "StoredProcedure";
		var statusCode = 503;
		var message = "Service temporarily unavailable";
		var innerException = new TimeoutException("Connection timed out");

		// Act
		var exception = new OperationFailedException(
			operation,
			resource,
			statusCode,
			message,
			innerException);

		// Assert
		exception.Operation.ShouldBe(operation);
		exception.Resource.ShouldBe(resource);
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	#endregion

	#region Alternative Constructor Tests (Resource only)

	[Fact]
	public void Create_WithResourceOnly_SetsResource()
	{
		// Arrange
		var resource = "DataTable";

		// Act
		var exception = new OperationFailedException(resource, null, null, null);

		// Assert
		exception.Resource.ShouldBe(resource);
	}

	[Fact]
	public void Create_WithResourceAndStatusCode_SetsStatusCode()
	{
		// Arrange
		var resource = "DataTable";
		var statusCode = 502;

		// Act
		var exception = new OperationFailedException(resource, statusCode, null, null);

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
	}

	#endregion

	#region Operation Property Tests

	[Fact]
	public void Operation_CanBeSet_AfterConstruction()
	{
		// Arrange
		var exception = new OperationFailedException("InitialOp", "Resource");

		// Act
		exception.Operation = "ModifiedOp";

		// Assert
		exception.Operation.ShouldBe("ModifiedOp");
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void Inherits_From_ResourceException()
	{
		// Arrange
		var exception = new OperationFailedException("Op", "Resource");

		// Assert
		exception.ShouldBeAssignableTo<Dispatch.Abstractions.ResourceException>();
	}

	[Fact]
	public void Inherits_From_Exception()
	{
		// Arrange
		var exception = new OperationFailedException("Op", "Resource");

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	#endregion
}
