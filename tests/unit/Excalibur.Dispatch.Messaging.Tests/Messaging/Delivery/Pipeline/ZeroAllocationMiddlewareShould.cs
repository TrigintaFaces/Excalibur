// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Pipeline;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for zero-allocation middleware result and traversal primitives.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Pipeline")]
[Trait("Priority", "0")]
public sealed class ZeroAllocationMiddlewareShould
{

	#region MiddlewareResult Tests

	[Fact]
	public void MiddlewareResult_Continue_ReturnsContinueExecution()
	{
		// Act
		var result = MiddlewareResult.Continue();

		// Assert
		result.ContinueExecution.ShouldBeTrue();
		result.Success.ShouldBeTrue();
		result.Error!.ShouldBeNull();
	}

	[Fact]
	public void MiddlewareResult_StopWithError_ReturnsStopExecution()
	{
		// Act
		var result = MiddlewareResult.StopWithError("Test error");

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeFalse();
		result.Error!.ShouldBe("Test error");
	}

	[Fact]
	public void MiddlewareResult_StopWithSuccess_ReturnsStopWithSuccess()
	{
		// Act
		var result = MiddlewareResult.StopWithSuccess();

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeTrue();
		result.Error!.ShouldBeNull();
	}

	[Fact]
	public void MiddlewareResult_Continue_HasNullErrorMessage()
	{
		// Act
		var result = MiddlewareResult.Continue();

		// Assert
		result.Error!.ShouldBeNull();
	}

	[Fact]
	public void MiddlewareResult_StopWithError_HasErrorMessage()
	{
		// Arrange
		const string errorMessage = "Custom error message";

		// Act
		var result = MiddlewareResult.StopWithError(errorMessage);

		// Assert
		result.Error!.ShouldBe(errorMessage);
	}

	#endregion

	#region MiddlewareContext Tests

	[Fact]
	public void MiddlewareContext_Constructor_InitializesWithMiddlewareArray()
	{
		// Arrange
		var middlewares = new IDispatchMiddleware[] { A.Fake<IDispatchMiddleware>() };

		// Act
		var context = new MiddlewareContext(middlewares);

		// Assert - MiddlewareContext is a struct, so we verify by checking its state
		context.CurrentIndex.ShouldBe(-1);
	}

	[Fact]
	public void MiddlewareContext_Constructor_WithEmptyArray_HasNoNext()
	{
		// Arrange & Act
		var context = new MiddlewareContext([]);

		// Assert
		context.HasNext.ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareContext_MoveNext_ReturnsFirstMiddleware()
	{
		// Arrange
		var middleware1 = A.Fake<IDispatchMiddleware>();
		var middlewares = new[] { middleware1 };
		var context = new MiddlewareContext(middlewares);

		// Act
		var result = context.MoveNext();

		// Assert
		result.ShouldBe(middleware1);
	}

	[Fact]
	public void MiddlewareContext_Reset_RestoresInitialState()
	{
		// Arrange
		var middleware1 = A.Fake<IDispatchMiddleware>();
		var middlewares = new[] { middleware1 };
		var context = new MiddlewareContext(middlewares);
		_ = context.MoveNext();

		// Act
		context.Reset();

		// Assert
		context.CurrentIndex.ShouldBe(-1);
		context.HasNext.ShouldBeTrue();
	}

	#endregion

}
