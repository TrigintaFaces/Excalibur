using Excalibur.Core.Exceptions;
using Excalibur.Domain.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain.Exceptions;

public class DomainExceptionShould
{
	[Fact]
	public void InheritFromApiException()
	{
		// Arrange & Act
		var exception = new DomainException(500);

		// Assert
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void SetStatusCodeFromConstructor()
	{
		// Arrange & Act
		var statusCode = 400;
		var exception = new DomainException(statusCode);

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
	}

	[Fact]
	public void UseDefaultMessageWhenNotProvided()
	{
		// Arrange & Act
		var exception = new DomainException(500);

		// Assert
		exception.Message.ShouldBe("Exception within application logic.");
	}

	[Fact]
	public void UseProvidedMessage()
	{
		// Arrange & Act
		var message = "Custom domain error message";
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new DomainException(500, message);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void IncludeInnerExceptionWhenProvided()
	{
		// Arrange
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var innerException = new InvalidOperationException("Inner exception message");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new DomainException(500, "Custom message", innerException);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void ThrowIfWhenConditionIsTrue()
	{
		// Arrange
		var condition = true;
		var statusCode = 404;
		var message = "Resource not found";

		// Act & Assert
		var exception = Should.Throw<DomainException>(() =>
			DomainException.ThrowIf(condition, statusCode, message));

		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void NotThrowWhenConditionIsFalse()
	{
		// Arrange
		var condition = false;
		var statusCode = 404;
		var message = "Resource not found";

		// Act & Assert
		Should.NotThrow(() => DomainException.ThrowIf(condition, statusCode, message));
	}

	[Fact]
	public void ThrowIfWithInnerExceptionWhenConditionIsTrue()
	{
		// Arrange
		var condition = true;
		var statusCode = 500;
		var message = "An error occurred";
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var innerException = new InvalidOperationException("Inner exception message");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act & Assert
		var exception = Should.Throw<DomainException>(() =>
			DomainException.ThrowIf(condition, statusCode, message, innerException));

		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void NotThrowWithInnerExceptionWhenConditionIsFalse()
	{
		// Arrange
		var condition = false;
		var statusCode = 500;
		var message = "An error occurred";
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var innerException = new InvalidOperationException("Inner exception message");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act & Assert
		Should.NotThrow(() => DomainException.ThrowIf(condition, statusCode, message, innerException));
	}
}
