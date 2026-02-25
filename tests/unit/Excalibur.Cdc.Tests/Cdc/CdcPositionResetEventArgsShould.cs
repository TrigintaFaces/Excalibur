// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcPositionResetEventArgs"/>.
/// Tests the CDC position reset event args model.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcPositionResetEventArgsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HasCorrectDefaults()
	{
		// Arrange & Act
		var args = new CdcPositionResetEventArgs();

		// Assert
		args.ProcessorId.ShouldBe(string.Empty);
		args.ProviderType.ShouldBe(string.Empty);
		args.CaptureInstance.ShouldBe(string.Empty);
		args.DatabaseName.ShouldBe(string.Empty);
		args.StalePosition.ShouldBeNull();
		args.NewPosition.ShouldBeNull();
		args.EarliestAvailablePosition.ShouldBeNull();
		args.LatestAvailablePosition.ShouldBeNull();
		args.ReasonCode.ShouldBe(string.Empty);
		args.ReasonMessage.ShouldBe(string.Empty);
		args.OriginalException.ShouldBeNull();
		args.AttemptNumber.ShouldBe(0);
		args.Strategy.ShouldBe(StalePositionRecoveryStrategy.Throw);
		args.AdditionalContext.ShouldBeNull();
		args.DetectedAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
	}

	#endregion

	#region Property Tests

	[Fact]
	public void ProcessorId_CanBeSet()
	{
		// Act
		var args = new CdcPositionResetEventArgs { ProcessorId = "processor-123" };

		// Assert
		args.ProcessorId.ShouldBe("processor-123");
	}

	[Fact]
	public void ProviderType_CanBeSet()
	{
		// Act
		var args = new CdcPositionResetEventArgs { ProviderType = "SqlServer" };

		// Assert
		args.ProviderType.ShouldBe("SqlServer");
	}

	[Fact]
	public void CaptureInstance_CanBeSet()
	{
		// Act
		var args = new CdcPositionResetEventArgs { CaptureInstance = "dbo_Orders" };

		// Assert
		args.CaptureInstance.ShouldBe("dbo_Orders");
	}

	[Fact]
	public void DatabaseName_CanBeSet()
	{
		// Act
		var args = new CdcPositionResetEventArgs { DatabaseName = "OrdersDb" };

		// Assert
		args.DatabaseName.ShouldBe("OrdersDb");
	}

	[Fact]
	public void StalePosition_CanBeSet()
	{
		// Arrange
		var position = new byte[] { 0x00, 0x01, 0x02, 0x03 };

		// Act
		var args = new CdcPositionResetEventArgs { StalePosition = position };

		// Assert
		args.StalePosition.ShouldBe(position);
	}

	[Fact]
	public void NewPosition_CanBeSet()
	{
		// Arrange
		var position = new byte[] { 0x00, 0x00, 0x00, 0x05 };

		// Act
		var args = new CdcPositionResetEventArgs { NewPosition = position };

		// Assert
		args.NewPosition.ShouldBe(position);
	}

	[Fact]
	public void EarliestAvailablePosition_CanBeSet()
	{
		// Arrange
		var position = new byte[] { 0x00, 0x00, 0x00, 0x01 };

		// Act
		var args = new CdcPositionResetEventArgs { EarliestAvailablePosition = position };

		// Assert
		args.EarliestAvailablePosition.ShouldBe(position);
	}

	[Fact]
	public void LatestAvailablePosition_CanBeSet()
	{
		// Arrange
		var position = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

		// Act
		var args = new CdcPositionResetEventArgs { LatestAvailablePosition = position };

		// Assert
		args.LatestAvailablePosition.ShouldBe(position);
	}

	[Fact]
	public void ReasonCode_CanBeSet()
	{
		// Act
		var args = new CdcPositionResetEventArgs { ReasonCode = "CDC_CLEANUP" };

		// Assert
		args.ReasonCode.ShouldBe("CDC_CLEANUP");
	}

	[Fact]
	public void ReasonMessage_CanBeSet()
	{
		// Act
		var args = new CdcPositionResetEventArgs { ReasonMessage = "The stored LSN is no longer available in CDC tables." };

		// Assert
		args.ReasonMessage.ShouldBe("The stored LSN is no longer available in CDC tables.");
	}

	[Fact]
	public void OriginalException_CanBeSet()
	{
		// Arrange
		var exception = new InvalidOperationException("LSN out of range");

		// Act
		var args = new CdcPositionResetEventArgs { OriginalException = exception };

		// Assert
		args.OriginalException.ShouldBe(exception);
	}

	[Fact]
	public void DetectedAt_CanBeSet()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var args = new CdcPositionResetEventArgs { DetectedAt = timestamp };

		// Assert
		args.DetectedAt.ShouldBe(timestamp);
	}

	[Fact]
	public void AttemptNumber_CanBeSet()
	{
		// Act
		var args = new CdcPositionResetEventArgs { AttemptNumber = 3 };

		// Assert
		args.AttemptNumber.ShouldBe(3);
	}

	[Fact]
	public void Strategy_CanBeSet()
	{
		// Act
		var args = new CdcPositionResetEventArgs { Strategy = StalePositionRecoveryStrategy.FallbackToEarliest };

		// Assert
		args.Strategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
	}

	[Fact]
	public void AdditionalContext_CanBeSet()
	{
		// Arrange
		var context = new Dictionary<string, object>
		{
			["ErrorNumber"] = 22037,
			["SqlState"] = "S0001"
		};

		// Act
		var args = new CdcPositionResetEventArgs { AdditionalContext = context };

		// Assert
		args.AdditionalContext.ShouldBe(context);
		args.AdditionalContext["ErrorNumber"].ShouldBe(22037);
	}

	#endregion

	#region Full Initialization Tests

	[Fact]
	public void CanBeFullyInitialized()
	{
		// Arrange
		var stalePosition = new byte[] { 0x00, 0x01, 0x02, 0x03 };
		var newPosition = new byte[] { 0x00, 0x00, 0x00, 0x01 };
		var earliestPosition = new byte[] { 0x00, 0x00, 0x00, 0x01 };
		var latestPosition = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
		var detectedAt = DateTimeOffset.UtcNow;
		var exception = new InvalidOperationException("Test");
		var context = new Dictionary<string, object> { ["Key"] = "Value" };

		// Act
		var args = new CdcPositionResetEventArgs
		{
			ProcessorId = "processor-123",
			ProviderType = "SqlServer",
			CaptureInstance = "dbo_Orders",
			DatabaseName = "OrdersDb",
			StalePosition = stalePosition,
			NewPosition = newPosition,
			EarliestAvailablePosition = earliestPosition,
			LatestAvailablePosition = latestPosition,
			ReasonCode = "CDC_CLEANUP",
			ReasonMessage = "Position is stale due to CDC cleanup",
			OriginalException = exception,
			DetectedAt = detectedAt,
			AttemptNumber = 2,
			Strategy = StalePositionRecoveryStrategy.FallbackToLatest,
			AdditionalContext = context
		};

		// Assert
		args.ProcessorId.ShouldBe("processor-123");
		args.ProviderType.ShouldBe("SqlServer");
		args.CaptureInstance.ShouldBe("dbo_Orders");
		args.DatabaseName.ShouldBe("OrdersDb");
		args.StalePosition.ShouldBe(stalePosition);
		args.NewPosition.ShouldBe(newPosition);
		args.EarliestAvailablePosition.ShouldBe(earliestPosition);
		args.LatestAvailablePosition.ShouldBe(latestPosition);
		args.ReasonCode.ShouldBe("CDC_CLEANUP");
		args.ReasonMessage.ShouldBe("Position is stale due to CDC cleanup");
		args.OriginalException.ShouldBe(exception);
		args.DetectedAt.ShouldBe(detectedAt);
		args.AttemptNumber.ShouldBe(2);
		args.Strategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToLatest);
		args.AdditionalContext.ShouldBe(context);
	}

	#endregion

	#region Strategy Tests

	[Theory]
	[InlineData(StalePositionRecoveryStrategy.Throw)]
	[InlineData(StalePositionRecoveryStrategy.FallbackToEarliest)]
	[InlineData(StalePositionRecoveryStrategy.FallbackToLatest)]
	[InlineData(StalePositionRecoveryStrategy.InvokeCallback)]
	public void Strategy_SupportsAllValues(StalePositionRecoveryStrategy strategy)
	{
		// Act
		var args = new CdcPositionResetEventArgs { Strategy = strategy };

		// Assert
		args.Strategy.ShouldBe(strategy);
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var args = new CdcPositionResetEventArgs
		{
			ProcessorId = "proc-1",
			ProviderType = "SqlServer",
			CaptureInstance = "dbo_Orders",
			ReasonCode = "CDC_CLEANUP",
			StalePosition = [0x00, 0x01],
			NewPosition = [0x00, 0x02],
			AttemptNumber = 1,
			Strategy = StalePositionRecoveryStrategy.FallbackToEarliest
		};

		// Act
		var result = args.ToString();

		// Assert
		result.ShouldContain("ProcessorId = proc-1");
		result.ShouldContain("ProviderType = SqlServer");
		result.ShouldContain("CaptureInstance = dbo_Orders");
		result.ShouldContain("ReasonCode = CDC_CLEANUP");
		result.ShouldContain("0x0001");
		result.ShouldContain("0x0002");
		result.ShouldContain("AttemptNumber = 1");
		result.ShouldContain("FallbackToEarliest");
	}

	[Fact]
	public void ToString_HandlesNullPositions()
	{
		// Arrange
		var args = new CdcPositionResetEventArgs
		{
			ProcessorId = "proc-1",
			CaptureInstance = "test"
		};

		// Act
		var result = args.ToString();

		// Assert
		result.ShouldContain("StalePosition = null");
		result.ShouldContain("NewPosition = null");
	}

	#endregion
}
