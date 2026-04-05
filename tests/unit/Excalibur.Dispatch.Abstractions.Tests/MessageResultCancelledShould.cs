// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="MessageResult.Cancelled()"/> and <see cref="MessageResult.Cancelled{T}()"/> factory methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageResultCancelledShould
{
	[Fact]
	public void Cancelled_ReturnsFailedResult()
	{
		// Act
		var result = MessageResult.Cancelled();

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void Cancelled_ReturnsResultWithNoErrorMessage()
	{
		// Act
		var result = MessageResult.Cancelled();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void Cancelled_ReturnsResultWithNoProblemDetails()
	{
		// Act
		var result = MessageResult.Cancelled();

		// Assert
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void Cancelled_ReturnsResultWithNoCacheHit()
	{
		// Act
		var result = MessageResult.Cancelled();

		// Assert
		result.CacheHit.ShouldBeFalse();
	}

	[Fact]
	public void CancelledT_ReturnsFailedResult()
	{
		// Act
		var result = MessageResult.Cancelled<int>();

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void CancelledT_ReturnsDefaultValue()
	{
		// Act
		var result = MessageResult.Cancelled<int>();

		// Assert
		result.ReturnValue.ShouldBe(default(int));
	}

	[Fact]
	public void CancelledT_WithReferenceType_ReturnsNullValue()
	{
		// Act
		var result = MessageResult.Cancelled<string>();

		// Assert
		result.ReturnValue.ShouldBeNull();
	}

	[Fact]
	public void CancelledT_ReturnsResultWithNoErrorMessage()
	{
		// Act
		var result = MessageResult.Cancelled<string>();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CancelledT_ReturnsResultWithNoProblemDetails()
	{
		// Act
		var result = MessageResult.Cancelled<string>();

		// Assert
		result.ProblemDetails.ShouldBeNull();
	}
}
