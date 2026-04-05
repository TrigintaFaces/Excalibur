// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="MessageResult.Failed(IMessageProblemDetails?, object?, object?)"/>
/// and exception-based <see cref="MessageResult.Failed(Exception)"/> / <see cref="MessageResult.Failed{T}(Exception)"/> factory methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageResultFailedContextShould
{
	// --- Failed(IMessageProblemDetails?, object?, object?) ---

	[Fact]
	public void Failed_WithProblemDetailsAndContext_ReturnsFailedResult()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problemDetails.Detail).Returns("Validation failed");
		var validationResult = new object();
		var authorizationResult = new object();

		// Act
		var result = MessageResult.Failed(problemDetails, validationResult, authorizationResult);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Validation failed");
		result.ProblemDetails.ShouldBe(problemDetails);
	}

	[Fact]
	public void Failed_WithProblemDetailsAndContext_SetsValidationResult()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problemDetails.Detail).Returns("Error");
		var validationResult = new object();

		// Act
		var result = MessageResult.Failed(problemDetails, validationResult, null);

		// Assert
		result.ValidationResult.ShouldBe(validationResult);
	}

	[Fact]
	public void Failed_WithProblemDetailsAndContext_SetsAuthorizationResult()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problemDetails.Detail).Returns("Error");
		var authorizationResult = new object();

		// Act
		var result = MessageResult.Failed(problemDetails, null, authorizationResult);

		// Assert
		result.AuthorizationResult.ShouldBe(authorizationResult);
	}

	[Fact]
	public void Failed_WithAllNullContext_ReturnsFailedWithNoDetails()
	{
		// Act
		var result = MessageResult.Failed(null, null, null);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBeNull();
		result.ProblemDetails.ShouldBeNull();
		result.ValidationResult.ShouldBeNull();
		result.AuthorizationResult.ShouldBeNull();
	}

	[Fact]
	public void Failed_WithOnlyValidationResult_SetsValidationOnly()
	{
		// Arrange
		var validationResult = new object();

		// Act
		var result = MessageResult.Failed(null, validationResult, null);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ValidationResult.ShouldBe(validationResult);
		result.AuthorizationResult.ShouldBeNull();
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void Failed_WithOnlyAuthorizationResult_SetsAuthorizationOnly()
	{
		// Arrange
		var authorizationResult = new object();

		// Act
		var result = MessageResult.Failed(null, null, authorizationResult);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.AuthorizationResult.ShouldBe(authorizationResult);
		result.ValidationResult.ShouldBeNull();
		result.ProblemDetails.ShouldBeNull();
	}

	// --- Failed(Exception) ---

	[Fact]
	public void Failed_WithException_ReturnsFailedResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Something broke");

		// Act
		var result = MessageResult.Failed(exception);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Something broke");
	}

	[Fact]
	public void Failed_WithException_IncludesExceptionType()
	{
		// Arrange
		var exception = new InvalidOperationException("test");

		// Act
		var result = MessageResult.Failed(exception);

		// Assert
		result.ErrorMessage.ShouldContain("InvalidOperationException");
	}

	[Fact]
	public void Failed_WithNullException_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => MessageResult.Failed((Exception)null!));
	}

	// --- Failed<T>(Exception) ---

	[Fact]
	public void FailedT_WithException_ReturnsFailedResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Something broke");

		// Act
		var result = MessageResult.Failed<int>(exception);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Something broke");
		result.ReturnValue.ShouldBe(default(int));
	}

	[Fact]
	public void FailedT_WithException_IncludesExceptionType()
	{
		// Arrange
		var exception = new ArgumentException("bad arg");

		// Act
		var result = MessageResult.Failed<string>(exception);

		// Assert
		result.ErrorMessage.ShouldContain("ArgumentException");
	}

	[Fact]
	public void FailedT_WithNullException_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => MessageResult.Failed<int>((Exception)null!));
	}
}
