// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.SqlServer.Cdc;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcPositionResetEventArgs"/>.
/// Tests event args properties and ToString formatting.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// bd-1710x: CDC Recovery Infrastructure Tests.
/// Updated: Uses consolidated core CdcPositionResetEventArgs.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "CdcPositionResetEventArgs")]
public sealed class StalePositionEventArgsShould : UnitTestBase
{
	[Fact]
	public void RequireProcessorId()
	{
		// Act & Assert - init property with default
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange
		};

		eventArgs.ProcessorId.ShouldBe("test-processor");
	}

	[Fact]
	public void RequireProviderType()
	{
		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.CdcCleanup
		};

		// Assert
		eventArgs.ProviderType.ShouldBe("SqlServer");
	}

	[Fact]
	public void RequireReasonCode()
	{
		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.CaptureInstanceDropped
		};

		// Assert
		eventArgs.ReasonCode.ShouldBe(StalePositionReasonCodes.CaptureInstanceDropped);
	}

	[Fact]
	public void AcceptOptionalStalePosition()
	{
		// Arrange
		var stalePosition = new byte[] { 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x01 };

		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange,
			StalePosition = stalePosition
		};

		// Assert
		eventArgs.StalePosition.ShouldBe(stalePosition);
	}

	[Fact]
	public void AcceptOptionalNewPosition()
	{
		// Arrange
		var newPosition = new byte[] { 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x01 };

		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange,
			NewPosition = newPosition
		};

		// Assert
		eventArgs.NewPosition.ShouldBe(newPosition);
	}

	[Fact]
	public void AcceptOptionalCaptureInstance()
	{
		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.CaptureInstanceDropped,
			CaptureInstance = "dbo_Orders"
		};

		// Assert
		eventArgs.CaptureInstance.ShouldBe("dbo_Orders");
	}

	[Fact]
	public void AcceptOptionalOriginalException()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.Unknown,
			OriginalException = exception
		};

		// Assert
		eventArgs.OriginalException.ShouldBe(exception);
	}

	[Fact]
	public void HaveDefaultDetectedAtTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange
		};

		var after = DateTimeOffset.UtcNow;

		// Assert
		eventArgs.DetectedAt.ShouldBeGreaterThanOrEqualTo(before);
		eventArgs.DetectedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void AcceptCustomDetectedAtTimestamp()
	{
		// Arrange
		var customTimestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange,
			DetectedAt = customTimestamp
		};

		// Assert
		eventArgs.DetectedAt.ShouldBe(customTimestamp);
	}

	[Fact]
	public void AcceptOptionalAdditionalContext()
	{
		// Arrange
		var context = new Dictionary<string, object>
		{
			["SqlErrorNumber"] = 22037,
			["DatabaseName"] = "TestDB"
		};

		// Act
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange,
			AdditionalContext = context
		};

		// Assert
		_ = eventArgs.AdditionalContext.ShouldNotBeNull();
		eventArgs.AdditionalContext["SqlErrorNumber"].ShouldBe(22037);
		eventArgs.AdditionalContext["DatabaseName"].ShouldBe("TestDB");
	}

	[Fact]
	public void FormatToStringWithAllProperties()
	{
		// Arrange
		var stalePosition = new byte[] { 0x00, 0x00, 0x01, 0x00 };
		var newPosition = new byte[] { 0x00, 0x00, 0x02, 0x00 };
		var detectedAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.LsnOutOfRange,
			StalePosition = stalePosition,
			NewPosition = newPosition,
			CaptureInstance = "dbo_Orders",
			DetectedAt = detectedAt
		};

		// Act
		var result = eventArgs.ToString();

		// Assert
		result.ShouldContain("ProcessorId = test-processor");
		result.ShouldContain("ProviderType = SqlServer");
		result.ShouldContain("ReasonCode = LSN_OUT_OF_RANGE");
		result.ShouldContain("0x00000100"); // StalePosition hex
		result.ShouldContain("0x00000200"); // NewPosition hex
		result.ShouldContain("CaptureInstance = dbo_Orders");
		result.ShouldContain("2025-01-15");
	}

	[Fact]
	public void FormatToStringWithNullPositions()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.CdcCleanup,
			StalePosition = null,
			NewPosition = null,
			CaptureInstance = string.Empty
		};

		// Act
		var result = eventArgs.ToString();

		// Assert
		result.ShouldContain("StalePosition = null");
		result.ShouldContain("NewPosition = null");
	}

	[Fact]
	public void FormatToStringWithCdcPositionResetEventArgsPrefix()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "test-processor",
			ProviderType = "SqlServer",
			ReasonCode = StalePositionReasonCodes.BackupRestore
		};

		// Act
		var result = eventArgs.ToString();

		// Assert
		result.ShouldStartWith("CdcPositionResetEventArgs {");
		result.ShouldEndWith("}");
	}
}
