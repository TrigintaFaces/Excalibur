// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Pipeline;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for <see cref="MiddlewareContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
[Trait("Priority", "0")]
public sealed class MiddlewareContextShould
{
	#region Constructor Tests

	[Fact]
	public void Construct_WithMiddlewareArray_SetsInitialState()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);

		// Act
		var context = new MiddlewareContext(middleware);

		// Assert
		context.CurrentIndex.ShouldBe(-1);
		context.HasNext.ShouldBeTrue();
	}

	[Fact]
	public void Construct_WithEmptyArray_HasNoNext()
	{
		// Arrange
		var middleware = Array.Empty<IDispatchMiddleware>();

		// Act
		var context = new MiddlewareContext(middleware);

		// Assert
		context.CurrentIndex.ShouldBe(-1);
		context.HasNext.ShouldBeFalse();
	}

	[Fact]
	public void Construct_WithSingleMiddleware_HasNext()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(1);

		// Act
		var context = new MiddlewareContext(middleware);

		// Assert
		context.HasNext.ShouldBeTrue();
	}

	#endregion

	#region MoveNext Tests

	[Fact]
	public void MoveNext_FromInitialState_ReturnsFirstMiddleware()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);
		var context = new MiddlewareContext(middleware);

		// Act
		var result = context.MoveNext();

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBe(middleware[0]);
		context.CurrentIndex.ShouldBe(0);
	}

	[Fact]
	public void MoveNext_IteratesThroughAllMiddleware()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);
		var context = new MiddlewareContext(middleware);

		// Act & Assert
		context.MoveNext().ShouldBe(middleware[0]);
		context.MoveNext().ShouldBe(middleware[1]);
		context.MoveNext().ShouldBe(middleware[2]);
		context.CurrentIndex.ShouldBe(2);
	}

	[Fact]
	public void MoveNext_AtEnd_ReturnsNull()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(2);
		var context = new MiddlewareContext(middleware);

		// Act
		_ = context.MoveNext();
		_ = context.MoveNext();
		var result = context.MoveNext();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void MoveNext_OnEmptyArray_ReturnsNull()
	{
		// Arrange
		var context = new MiddlewareContext(Array.Empty<IDispatchMiddleware>());

		// Act
		var result = context.MoveNext();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void MoveNext_AfterEnd_ContinuesToReturnNull()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(1);
		var context = new MiddlewareContext(middleware);

		// Act
		_ = context.MoveNext();
		var result1 = context.MoveNext();
		var result2 = context.MoveNext();

		// Assert
		result1.ShouldBeNull();
		result2.ShouldBeNull();
	}

	#endregion

	#region HasNext Tests

	[Fact]
	public void HasNext_BeforeAnyIteration_IsTrue()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);
		var context = new MiddlewareContext(middleware);

		// Assert
		context.HasNext.ShouldBeTrue();
	}

	[Fact]
	public void HasNext_AfterFirstMoveNext_IsTrue()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);
		var context = new MiddlewareContext(middleware);

		// Act
		_ = context.MoveNext();

		// Assert
		context.HasNext.ShouldBeTrue();
	}

	[Fact]
	public void HasNext_AtLastMiddleware_IsFalse()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);
		var context = new MiddlewareContext(middleware);

		// Act
		_ = context.MoveNext();
		_ = context.MoveNext();
		_ = context.MoveNext();

		// Assert
		context.HasNext.ShouldBeFalse();
	}

	[Fact]
	public void HasNext_WithSingleMiddleware_IsFalseAfterFirst()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(1);
		var context = new MiddlewareContext(middleware);

		// Act
		_ = context.MoveNext();

		// Assert
		context.HasNext.ShouldBeFalse();
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_SetsCurrentIndexToMinusOne()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);
		var context = new MiddlewareContext(middleware);
		_ = context.MoveNext();
		_ = context.MoveNext();

		// Act
		context.Reset();

		// Assert
		context.CurrentIndex.ShouldBe(-1);
	}

	[Fact]
	public void Reset_AllowsReiterationFromBeginning()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);
		var context = new MiddlewareContext(middleware);
		_ = context.MoveNext();
		_ = context.MoveNext();
		_ = context.MoveNext();

		// Act
		context.Reset();
		var result = context.MoveNext();

		// Assert
		result.ShouldBe(middleware[0]);
		context.HasNext.ShouldBeTrue();
	}

	[Fact]
	public void Reset_OnEmptyArray_SetsIndexToMinusOne()
	{
		// Arrange
		var context = new MiddlewareContext(Array.Empty<IDispatchMiddleware>());
		_ = context.MoveNext();

		// Act
		context.Reset();

		// Assert
		context.CurrentIndex.ShouldBe(-1);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameState_ReturnsTrue()
	{
		// Arrange
		var middleware1 = CreateMiddlewareArray(3);
		var middleware2 = CreateMiddlewareArray(3);
		var context1 = new MiddlewareContext(middleware1);
		var context2 = new MiddlewareContext(middleware2);

		// Assert
		context1.Equals(context2).ShouldBeTrue();
		(context1 == context2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentCounts_ReturnsFalse()
	{
		// Arrange
		var context1 = new MiddlewareContext(CreateMiddlewareArray(3));
		var context2 = new MiddlewareContext(CreateMiddlewareArray(5));

		// Assert
		context1.Equals(context2).ShouldBeFalse();
		(context1 != context2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentIndices_ReturnsFalse()
	{
		// Arrange
		var context1 = new MiddlewareContext(CreateMiddlewareArray(3));
		var context2 = new MiddlewareContext(CreateMiddlewareArray(3));
		_ = context1.MoveNext();

		// Assert
		context1.Equals(context2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_ReturnsCorrectResult()
	{
		// Arrange
		var context1 = new MiddlewareContext(CreateMiddlewareArray(3));
		object context2 = new MiddlewareContext(CreateMiddlewareArray(3));
		object notContext = "not a context";

		// Assert
		context1.Equals(context2).ShouldBeTrue();
		context1.Equals(notContext).ShouldBeFalse();
		context1.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_WithSameState_ReturnsSameValue()
	{
		// Arrange
		var context1 = new MiddlewareContext(CreateMiddlewareArray(3));
		var context2 = new MiddlewareContext(CreateMiddlewareArray(3));

		// Assert
		context1.GetHashCode().ShouldBe(context2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_AfterMoveNext_ChangesValue()
	{
		// Arrange
		var context = new MiddlewareContext(CreateMiddlewareArray(3));
		var hashBefore = context.GetHashCode();

		// Act
		_ = context.MoveNext();
		var hashAfter = context.GetHashCode();

		// Assert
		hashBefore.ShouldNotBe(hashAfter);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Context_ForPipelineExecution_IteratesAllMiddleware()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(5);
		var context = new MiddlewareContext(middleware);
		var executedMiddleware = new List<IDispatchMiddleware>();

		// Act
		while (context.MoveNext() is { } current)
		{
			executedMiddleware.Add(current);
		}

		// Assert
		executedMiddleware.Count.ShouldBe(5);
		executedMiddleware.ShouldBe(middleware);
	}

	[Fact]
	public void Context_ForPartialExecution_StopsAtCorrectPoint()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(5);
		var context = new MiddlewareContext(middleware);

		// Act - Execute only first 3 middleware
		_ = context.MoveNext();
		_ = context.MoveNext();
		_ = context.MoveNext();

		// Assert
		context.CurrentIndex.ShouldBe(2);
		context.HasNext.ShouldBeTrue();
	}

	[Fact]
	public void Context_ForRetry_CanBeReset()
	{
		// Arrange
		var middleware = CreateMiddlewareArray(3);
		var context = new MiddlewareContext(middleware);

		// First execution
		while (context.MoveNext() is not null)
		{
			// Iterate through all
		}
		context.HasNext.ShouldBeFalse();

		// Act - Reset for retry
		context.Reset();

		// Assert
		context.HasNext.ShouldBeTrue();
		context.MoveNext().ShouldBe(middleware[0]);
	}

	#endregion

	#region Helper Methods

	private static IDispatchMiddleware[] CreateMiddlewareArray(int count)
	{
		var middleware = new IDispatchMiddleware[count];
		for (var i = 0; i < count; i++)
		{
			middleware[i] = A.Fake<IDispatchMiddleware>();
		}

		return middleware;
	}

	#endregion
}
