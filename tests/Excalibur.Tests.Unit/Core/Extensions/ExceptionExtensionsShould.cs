using Excalibur.Core.Exceptions;
using Excalibur.Core.Extensions;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Extensions;

public class ExceptionExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullExceptionIfExceptionIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => ((ApiException)null!).GetErrorCode());
		_ = Should.Throw<ArgumentNullException>(() => ((ApiException)null!).GetStatusCode());
	}

	[Fact]
	public void ExtractStatusCodeFromApiException()
	{
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new ApiException(400, "Bad Request", null);
#pragma warning restore CA1303 // Do not pass literals as localized parameters
		exception.GetStatusCode().ShouldBe(400);
	}

	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
		Justification = "Unit Testing")]
	public void PropagateStatusCodeFromInnerException()
	{
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var innerException = new ApiException(403, "Forbidden", null);
#pragma warning restore CA1303 // Do not pass literals as localized parameters
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var outerException = new Exception("Wrapper", innerException);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		outerException.GetStatusCode().ShouldBe(403);
	}

	[Fact]
	public void ReturnErrorCodeFromPropertyIfExists()
	{
		// Arrange
		var exception = new CustomApiExceptionWithErrorCode();

		// Act
		var errorCode = exception.GetErrorCode();

		// Assert
		errorCode.ShouldBe(404);
	}

	[Fact]
	public void ReturnErrorCodeFromDataIfExists()
	{
		// Arrange
		var exception = new ApiException();
		exception.Data["ErrorCode"] = 500;

		// Act
		var errorCode = exception.GetErrorCode();

		// Assert
		errorCode.ShouldBe(500);
	}

	[Fact]
	public void ReturnErrorCodeFromInnerException()
	{
		// Arrange
		var innerException = new ApiException();
		innerException.Data["ErrorCode"] = 403;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var outerException = new ApiException("Outer exception", innerException);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		var errorCode = outerException.GetErrorCode();

		// Assert
		errorCode.ShouldBe(403);
	}

	[Fact]
	public void ReturnDefaultErrorCodeIfNotFound()
	{
		// Arrange
		var exception = new ApiException();

		// Act
		var errorCode = exception.GetErrorCode();

		// Assert
		errorCode.ShouldBe(-1);
	}

	[Fact]
	public void ReturnStatusCodeFromPropertyIfExists()
	{
		// Arrange
		var exception = new CustomApiExceptionWithStatusCode();

		// Act
		var statusCode = exception.GetStatusCode();

		// Assert
		statusCode.ShouldBe(404);
	}

	[Fact]
	public void ReturnStatusCodeFromDataIfExists()
	{
		// Arrange
		var exception = new ApiException();
		exception.Data["StatusCode"] = 502;

		// Act
		var statusCode = exception.GetStatusCode();

		// Assert
		statusCode.ShouldBe(502);
	}

	[Fact]
	public void ReturnStatusCodeFromInnerException()
	{
		// Arrange
		var innerException = new ApiException();
		innerException.Data["StatusCode"] = 401;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var outerException = new ApiException("Outer exception", innerException);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Act
		var statusCode = outerException.GetStatusCode();

		// Assert
		statusCode.ShouldBe(401);
	}

	[Fact]
	public void ReturnDefaultStatusCodeIfNotFound()
	{
		// Arrange
		var exception = new ApiException();

		// Act
		var statusCode = exception.GetStatusCode();

		// Assert
		statusCode.ShouldBe(500);
	}
}

// Custom exception classes for testing
public class CustomApiExceptionWithErrorCode : ApiException
{
	public int ErrorCode => 404;
}

public class CustomApiExceptionWithStatusCode : ApiException
{
	public int StatusCode => 404;
}
