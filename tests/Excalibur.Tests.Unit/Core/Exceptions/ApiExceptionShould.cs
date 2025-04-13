using Excalibur.Core.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Exceptions;

public class ApiExceptionShould
{
	[Fact]
	public void InitializeWithDefaultMessageAndStatusCode()
	{
		// Arrange & Act
		var exception = new ApiException();

		// Assert
		exception.Message.ShouldBe("An unexpected error occurred");
		exception.StatusCode.ShouldBe(500);
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void InitializeWithCustomMessage()
	{
		// Arrange
		var message = "Custom error message";

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new ApiException(message, null);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.Message.ShouldBe(message);
		exception.StatusCode.ShouldBe(500);
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void InitializeWithCustomMessageAndInnerException()
	{
		// Arrange
		var message = "Custom error message";
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var innerException = new InvalidOperationException("Inner exception message");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new ApiException(message, innerException);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.StatusCode.ShouldBe(500);
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void InitializeWithCustomStatusCodeMessageAndInnerException()
	{
		// Arrange
		var statusCode = 404;
		var message = "Resource not found";
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var innerException = new InvalidOperationException("Inner exception message");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new ApiException(statusCode, message, innerException);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void ThrowArgumentOutOfRangeExceptionForInvalidStatusCode()
	{
		// Arrange
		var invalidStatusCode = 700;

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new ApiException(invalidStatusCode, "Invalid status code", null)).ParamName
			.ShouldBe("statusCode");
	}

	[Fact]
	public void UseDefaultMessageIfNullMessageProvided()
	{
		// Arrange
		var statusCode = 400;

		// Act
		var exception = new ApiException(statusCode, null, null);

		// Assert
		exception.Message.ShouldBe("An unexpected error occurred");
		exception.StatusCode.ShouldBe(statusCode);
	}

	[Fact]
	public void GenerateUniqueIdsForDifferentInstances()
	{
		// Arrange & Act
		var exception1 = new ApiException();
		var exception2 = new ApiException();

		// Assert
		exception1.Id.ShouldNotBe(exception2.Id);
	}

	[Fact]
	public void ReturnCorrectStatusCodeFromStaticMethod()
	{
		// Arrange
		var statusCode = 401;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new ApiException(statusCode, "Unauthorized", null);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		var result = ApiException.GetStatusCode(exception);

		// Assert
		result.ShouldBe(statusCode);
	}

	[Fact]
	public void Return500ForNonApiExceptionInGetStatusCode()
	{
		// Arrange
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new InvalidOperationException("Not an API exception");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		var result = ApiException.GetStatusCode(exception);

		// Assert
		result.ShouldBe(500);
	}
}
