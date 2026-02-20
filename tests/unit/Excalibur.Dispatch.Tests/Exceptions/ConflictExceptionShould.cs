// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConflictExceptionShould
{
	[Fact]
	public void InitializeWithDefaultConstructor()
	{
		// Act
		var ex = new ConflictException();

		// Assert
		ex.DispatchStatusCode.ShouldBe(409);
		ex.Message.ShouldContain("conflict");
	}

	[Fact]
	public void InitializeWithMessage()
	{
		// Act
		var ex = new ConflictException("Duplicate detected");

		// Assert
		ex.Message.ShouldBe("Duplicate detected");
		ex.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void InitializeWithMessageAndInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("inner");

		// Act
		var ex = new ConflictException("outer", inner);

		// Assert
		ex.Message.ShouldBe("outer");
		ex.InnerException.ShouldBe(inner);
		ex.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void InitializeWithResourceFieldAndReason()
	{
		// Act
		var ex = new ConflictException("User", "email", "Email already registered");

		// Assert
		ex.Resource.ShouldBe("User");
		ex.Field.ShouldBe("email");
		ex.Reason.ShouldBe("Email already registered");
		ex.DispatchStatusCode.ShouldBe(409);
		ex.Context["resource"].ShouldBe("User");
		ex.Context["field"].ShouldBe("email");
		ex.Context["reason"].ShouldBe("Email already registered");
	}

	[Fact]
	public void CreateWithAlreadyExistsFactory()
	{
		// Act
		var ex = ConflictException.AlreadyExists("User", "user@example.com");

		// Assert
		ex.Message.ShouldContain("already exists");
		ex.Message.ShouldContain("user@example.com");
		ex.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void CreateWithInvalidStateTransitionFactory()
	{
		// Act
		var ex = ConflictException.InvalidStateTransition("Order", "Pending", "Shipped");

		// Assert
		ex.Message.ShouldContain("Pending");
		ex.Message.ShouldContain("Shipped");
		ex.Context["currentState"].ShouldBe("Pending");
		ex.Context["targetState"].ShouldBe("Shipped");
		ex.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void CreateWithReasonFactory()
	{
		// Act
		var ex = ConflictException.WithReason("Order", "Order is already completed");

		// Assert
		ex.Resource.ShouldBe("Order");
		ex.Reason.ShouldBe("Order is already completed");
		ex.Message.ShouldContain("Order");
		ex.Message.ShouldContain("Order is already completed");
		ex.Context["resource"].ShouldBe("Order");
		ex.Context["reason"].ShouldBe("Order is already completed");
	}

	[Fact]
	public void ProduceCorrectProblemDetailsExtensions()
	{
		// Arrange
		var ex = new ConflictException("User", "email", "Email taken");

		// Act
		var details = ex.ToProblemDetails();

		// Assert
		details.ShouldNotBeNull();
		details.Status.ShouldBe(409);
	}
}
