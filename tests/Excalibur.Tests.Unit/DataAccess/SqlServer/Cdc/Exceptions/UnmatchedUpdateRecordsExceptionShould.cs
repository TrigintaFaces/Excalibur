using Excalibur.Core.Exceptions;
using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc.Exceptions;

public class UnmatchedUpdateRecordsExceptionShould
{
	[Fact]
	public void CreateWithDefaultMessageShouldIncludeHexLsn()
	{
		// Arrange
		var lsn = new byte[] { 0x01, 0x02, 0x03 };

		// Act
		var exception = new UnmatchedUpdateRecordsException(lsn);

		// Assert
		exception.StatusCode.ShouldBe(500);
		exception.Lsn.ShouldBe(lsn);
		exception.Message.ShouldContain("Unmatched UpdateBefore/UpdateAfter pairs detected");
		exception.Message.ShouldContain("010203"); // Hex content from LSN
	}

	[Fact]
	public void CreateWithCustomMessageAndStatusCodeShouldOverrideDefaults()
	{
		// Arrange
		var lsn = new byte[] { 0xAA, 0xBB, 0xCC };
		const int customStatus = 422;
		const string customMessage = "Custom mismatch at LSN";

		// Act
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var exception = new UnmatchedUpdateRecordsException(lsn, customStatus, customMessage);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		// Assert
		exception.StatusCode.ShouldBe(customStatus);
		exception.Message.ShouldBe(customMessage);
		exception.Lsn.ShouldBe(lsn);
	}

	[Fact]
	public void ConstructorShouldThrowIfLsnIsNull()
	{
		// Act + Assert
		_ = Should.Throw<ArgumentNullException>(() => new UnmatchedUpdateRecordsException(null!));
	}

	[Fact]
	public void ShouldInheritFromApiException()
	{
		// Act
		var exception = new UnmatchedUpdateRecordsException([0x01]);

		// Assert
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}
}
