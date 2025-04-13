using Excalibur.Core.Exceptions;
using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc.Exceptions;

public class CdcMissingTableHandlerExceptionShould
{
	[Fact]
	public void CreateWithDefaultMessageShouldSetStatusCodeAndMessage()
	{
		// Arrange
		var tableName = "Customers";

		// Act
		var exception = new CdcMissingTableHandlerException(tableName);

		// Assert
		exception.StatusCode.ShouldBe(500);
		exception.TableName.ShouldBe(tableName);
		exception.Message.ShouldBe($"No IDataChangeHandler implementation found for table {tableName}.");
	}

	[Fact]
	public void CreateWithCustomMessageAndStatusCodeShouldOverrideDefaults()
	{
		// Arrange
		var tableName = "Orders";
		var statusCode = 404;
		var message = "Handler not registered";

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new CdcMissingTableHandlerException(tableName, statusCode, message);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.TableName.ShouldBe(tableName);
	}

	[Fact]
	public void ThrowIfTableNameIsNullOrWhitespace()
	{
		// Arrange + Act + Assert
		_ = Should.Throw<ArgumentException>(() => new CdcMissingTableHandlerException(null!));
		_ = Should.Throw<ArgumentException>(() => new CdcMissingTableHandlerException(""));
		_ = Should.Throw<ArgumentException>(() => new CdcMissingTableHandlerException("   "));
	}

	[Fact]
	public void InheritsFromApiException()
	{
		// Arrange
		var exception = new CdcMissingTableHandlerException("TestTable");

		// Act + Assert
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}
}
