// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for CDC exception types.
/// Tests message formatting and property validation.
/// </summary>
/// <remarks>
/// Sprint 201 - Unit Test Coverage Epic.
/// Excalibur.Dispatch-7dm: CDC Unit Tests.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "CdcExceptions")]
public sealed class CdcExceptionsShould : UnitTestBase
{
	#region CdcMissingTableHandlerException Tests

	[Fact]
	public void CdcMissingTableHandlerException_ThrowArgumentException_WhenTableNameIsNullOrWhiteSpace()
	{
		// Act & Assert - Note: must use the constructor with optional params to trigger validation
		_ = Should.Throw<ArgumentException>(() => new CdcMissingTableHandlerException(null!, statusCode: null));
		_ = Should.Throw<ArgumentException>(() => new CdcMissingTableHandlerException("", statusCode: null));
		_ = Should.Throw<ArgumentException>(() => new CdcMissingTableHandlerException("   ", statusCode: null));
	}

	[Fact]
	public void CdcMissingTableHandlerException_SetTableNameProperty()
	{
		// Arrange & Act - Use the constructor with optional params that sets TableName
		var exception = new CdcMissingTableHandlerException("TestTable", statusCode: null);

		// Assert
		exception.TableName.ShouldBe("TestTable");
	}

	[Fact]
	public void CdcMissingTableHandlerException_FormatDefaultMessage()
	{
		// Arrange & Act - Use the constructor with optional params for full message formatting
		var exception = new CdcMissingTableHandlerException("TestTable", statusCode: null);

		// Assert
		exception.Message.ShouldContain("IDataChangeHandler");
		exception.Message.ShouldContain("TestTable");
	}

	[Fact]
	public void CdcMissingTableHandlerException_UseDefaultStatusCode()
	{
		// Arrange & Act
		var exception = new CdcMissingTableHandlerException("TestTable", statusCode: null);

		// Assert
		exception.StatusCode.ShouldBe(500);
	}

	#endregion CdcMissingTableHandlerException Tests

	#region CdcMultipleTableHandlerException Tests

	[Fact]
	public void CdcMultipleTableHandlerException_ThrowArgumentException_WhenTableNameIsNullOrWhiteSpace()
	{
		// Act & Assert - Note: must use the constructor with optional params to trigger validation
		_ = Should.Throw<ArgumentException>(() => new CdcMultipleTableHandlerException(null!, statusCode: null));
		_ = Should.Throw<ArgumentException>(() => new CdcMultipleTableHandlerException("", statusCode: null));
		_ = Should.Throw<ArgumentException>(() => new CdcMultipleTableHandlerException("   ", statusCode: null));
	}

	[Fact]
	public void CdcMultipleTableHandlerException_SetTableNameProperty()
	{
		// Arrange & Act - Use the constructor with optional params that sets TableName
		var exception = new CdcMultipleTableHandlerException("TestTable", statusCode: null);

		// Assert
		exception.TableName.ShouldBe("TestTable");
	}

	[Fact]
	public void CdcMultipleTableHandlerException_FormatDefaultMessage()
	{
		// Arrange & Act - Use the constructor with optional params for full message formatting
		var exception = new CdcMultipleTableHandlerException("TestTable", statusCode: null);

		// Assert
		exception.Message.ShouldContain("Multiple");
		exception.Message.ShouldContain("IDataChangeHandler");
		exception.Message.ShouldContain("TestTable");
	}

	#endregion CdcMultipleTableHandlerException Tests

	#region CdcStalePositionException Tests

	[Fact]
	public void CdcStalePositionException_DefaultConstructor_SetDefaultMessage()
	{
		// Arrange & Act
		var exception = new CdcStalePositionException();

		// Assert
		exception.Message.ShouldContain("stale position");
		exception.Message.ShouldContain("cannot be recovered");
	}

	[Fact]
	public void CdcStalePositionException_WithEventArgs_SetEventArgsProperty()
	{
		// Arrange
		var eventArgs = CreateTestEventArgs();

		// Act
		var exception = new SqlServerCdcStalePositionException(eventArgs);

		// Assert
		exception.EventArgs.ShouldBe(eventArgs);
		_ = exception.StalePosition.ShouldNotBeNull();
		exception.ReasonCode.ShouldBe(StalePositionReasonCodes.LsnOutOfRange);
		exception.CaptureInstance.ShouldBe("dbo_TestTable");
	}

	[Fact]
	public void CdcStalePositionException_WithEventArgs_AcceptsNullEventArgs()
	{
		// Act - FormatMessage handles null gracefully, returns default message
		var exception = new CdcStalePositionException((CdcPositionResetEventArgs)null!);

		// Assert
		exception.Message.ShouldContain("stale position");
		exception.EventArgs.ShouldBeNull();
	}

	[Fact]
	public void CdcStalePositionException_WithEventArgs_FormatMessageWithDetails()
	{
		// Arrange
		var eventArgs = CreateTestEventArgs();

		// Act
		var exception = new CdcStalePositionException(eventArgs);

		// Assert
		exception.Message.ShouldContain("0x");
		exception.Message.ShouldContain(StalePositionReasonCodes.LsnOutOfRange);
		exception.Message.ShouldContain("test-processor");
	}

	[Fact]
	public void CdcStalePositionException_InheritFromResourceException()
	{
		// Sprint 698 T.56: CdcStalePositionException reparented to ResourceException
		var exception = new CdcStalePositionException();
		_ = exception.ShouldBeAssignableTo<Excalibur.Data.Abstractions.ResourceException>();
	}

	[Fact]
	public void CdcMappingException_InheritFromResourceException()
	{
		// Sprint 698 T.56: CdcMappingException reparented to ResourceException
		var exception = new CdcMappingException("test mapping error");
		_ = exception.ShouldBeAssignableTo<Excalibur.Data.Abstractions.ResourceException>();
	}

	#endregion CdcStalePositionException Tests

	private static CdcPositionResetEventArgs CreateTestEventArgs()
	{
		return new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange,
			StalePosition = [0x00, 0x00, 0x00, 0x01],
			NewPosition = [0x00, 0x00, 0x00, 0x02],
			CaptureInstance = "dbo_TestTable"
		};
	}
}
