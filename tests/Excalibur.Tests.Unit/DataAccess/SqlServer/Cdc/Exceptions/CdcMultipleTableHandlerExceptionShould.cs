using Excalibur.Core.Exceptions;
using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc.Exceptions;

public class CdcMultipleTableHandlerExceptionShould
{
	[Fact]
	public void CreateWithDefaultMessageShouldSetMessageAndStatusCode()
	{
		// Arrange
		var tableName = "Orders";

		// Act
		var exception = new CdcMultipleTableHandlerException(tableName);

		// Assert
		exception.StatusCode.ShouldBe(500);
		exception.TableName.ShouldBe(tableName);
		exception.Message.ShouldBe(
			$"Multiple IDataChangeHandler implementations found for table {tableName}. Ensure that only one handler is registered per table.");
	}

	[Fact]
	public void CreateWithCustomMessageAndStatusCodeShouldOverrideDefaults()
	{
		// Arrange
		var tableName = "Products";
		var statusCode = 409;
		var message = "Duplicate handler found";

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new CdcMultipleTableHandlerException(tableName, statusCode, message);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.TableName.ShouldBe(tableName);
	}

	[Fact]
	public void ConstructorShouldThrowIfTableNameIsNullOrWhitespace()
	{
		// Arrange + Assert
		_ = Should.Throw<ArgumentException>(() => new CdcMultipleTableHandlerException(null!));
		_ = Should.Throw<ArgumentException>(() => new CdcMultipleTableHandlerException(""));
		_ = Should.Throw<ArgumentException>(() => new CdcMultipleTableHandlerException("  "));
	}

	[Fact]
	public void ShouldInheritFromApiException()
	{
		// Act
		var exception = new CdcMultipleTableHandlerException("Invoices");

		// Assert
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}
}
