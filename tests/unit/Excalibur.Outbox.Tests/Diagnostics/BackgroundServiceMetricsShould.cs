// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Diagnostics;

namespace Excalibur.Outbox.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="BackgroundServiceMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class BackgroundServiceMetricsShould : UnitTestBase
{
	#region Constants Tests

	[Fact]
	public void HaveCorrectMeterName()
	{
		// Assert
		BackgroundServiceMetrics.MeterName.ShouldBe("Excalibur.Dispatch.BackgroundServices");
	}

	[Fact]
	public void HaveCorrectMeterVersion()
	{
		// Assert
		BackgroundServiceMetrics.MeterVersion.ShouldBe("1.0.0");
	}

	#endregion Constants Tests

	#region RecordMessagesProcessed Tests

	[Fact]
	public void RecordMessagesProcessed_NotThrowForValidInput()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesProcessed("outbox", "pending", 10));
	}

	[Fact]
	public void RecordMessagesProcessed_NotThrowForZeroCount()
	{
		// Act & Assert - Should not throw (should be ignored)
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesProcessed("outbox", "pending", 0));
	}

	[Fact]
	public void RecordMessagesProcessed_NotThrowForNegativeCount()
	{
		// Act & Assert - Should not throw (should be ignored)
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesProcessed("outbox", "pending", -1));
	}

	[Fact]
	public void RecordMessagesProcessed_NotThrowForLargeCount()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesProcessed("outbox", "pending", 1_000_000));
	}

	[Fact]
	public void RecordMessagesProcessed_AcceptOutboxServiceType()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Outbox, "pending", 5));
	}

	[Fact]
	public void RecordMessagesProcessed_AcceptInboxServiceType()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Inbox, "dispatch", 5));
	}

	[Fact]
	public void RecordMessagesProcessed_AcceptCdcServiceType()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesProcessed(BackgroundServiceTypes.Cdc, "pending", 5));
	}

	#endregion RecordMessagesProcessed Tests

	#region RecordMessagesFailed Tests

	[Fact]
	public void RecordMessagesFailed_NotThrowForValidInput()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesFailed("outbox", "pending", 5));
	}

	[Fact]
	public void RecordMessagesFailed_NotThrowForZeroCount()
	{
		// Act & Assert - Should not throw (should be ignored)
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesFailed("outbox", "pending", 0));
	}

	[Fact]
	public void RecordMessagesFailed_NotThrowForNegativeCount()
	{
		// Act & Assert - Should not throw (should be ignored)
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesFailed("inbox", "dispatch", -10));
	}

	[Fact]
	public void RecordMessagesFailed_AcceptAllOperationTypes()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesFailed("outbox", BackgroundServiceOperations.Pending, 1));
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesFailed("outbox", BackgroundServiceOperations.Scheduled, 1));
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesFailed("outbox", BackgroundServiceOperations.Retry, 1));
		Should.NotThrow(() => BackgroundServiceMetrics.RecordMessagesFailed("inbox", BackgroundServiceOperations.Dispatch, 1));
	}

	#endregion RecordMessagesFailed Tests

	#region RecordProcessingDuration Tests

	[Fact]
	public void RecordProcessingDuration_NotThrowForValidInput()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingDuration("outbox", 100.5));
	}

	[Fact]
	public void RecordProcessingDuration_AcceptZeroDuration()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingDuration("outbox", 0.0));
	}

	[Fact]
	public void RecordProcessingDuration_AcceptSmallDuration()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingDuration("inbox", 0.001));
	}

	[Fact]
	public void RecordProcessingDuration_AcceptLargeDuration()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingDuration("cdc", 60000.0));
	}

	#endregion RecordProcessingDuration Tests

	#region RecordProcessingCycle Tests

	[Fact]
	public void RecordProcessingCycle_NotThrowForValidInput()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingCycle("outbox", "success"));
	}

	[Fact]
	public void RecordProcessingCycle_AcceptSuccessResult()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingCycle("outbox", BackgroundServiceResults.Success));
	}

	[Fact]
	public void RecordProcessingCycle_AcceptPartialResult()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingCycle("outbox", BackgroundServiceResults.Partial));
	}

	[Fact]
	public void RecordProcessingCycle_AcceptEmptyResult()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingCycle("inbox", BackgroundServiceResults.Empty));
	}

	[Fact]
	public void RecordProcessingCycle_AcceptErrorResult()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingCycle("cdc", BackgroundServiceResults.Error));
	}

	#endregion RecordProcessingCycle Tests

	#region RecordProcessingError Tests

	[Fact]
	public void RecordProcessingError_NotThrowForValidInput()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingError("outbox", "TimeoutException"));
	}

	[Fact]
	public void RecordProcessingError_AcceptVariousErrorTypes()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingError("outbox", "InvalidOperationException"));
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingError("inbox", "ArgumentNullException"));
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingError("cdc", "DatabaseException"));
	}

	[Fact]
	public void RecordProcessingError_AcceptAllServiceTypes()
	{
		// Act & Assert
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingError(BackgroundServiceTypes.Outbox, "Error"));
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingError(BackgroundServiceTypes.Inbox, "Error"));
		Should.NotThrow(() => BackgroundServiceMetrics.RecordProcessingError(BackgroundServiceTypes.Cdc, "Error"));
	}

	#endregion RecordProcessingError Tests
}

/// <summary>
/// Unit tests for <see cref="BackgroundServiceTypes"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class BackgroundServiceTypesShould : UnitTestBase
{
	[Fact]
	public void HaveOutboxConstant()
	{
		// Assert
		BackgroundServiceTypes.Outbox.ShouldBe("outbox");
	}

	[Fact]
	public void HaveInboxConstant()
	{
		// Assert
		BackgroundServiceTypes.Inbox.ShouldBe("inbox");
	}

	[Fact]
	public void HaveCdcConstant()
	{
		// Assert
		BackgroundServiceTypes.Cdc.ShouldBe("cdc");
	}
}

/// <summary>
/// Unit tests for <see cref="BackgroundServiceOperations"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class BackgroundServiceOperationsShould : UnitTestBase
{
	[Fact]
	public void HavePendingConstant()
	{
		// Assert
		BackgroundServiceOperations.Pending.ShouldBe("pending");
	}

	[Fact]
	public void HaveScheduledConstant()
	{
		// Assert
		BackgroundServiceOperations.Scheduled.ShouldBe("scheduled");
	}

	[Fact]
	public void HaveRetryConstant()
	{
		// Assert
		BackgroundServiceOperations.Retry.ShouldBe("retry");
	}

	[Fact]
	public void HaveDispatchConstant()
	{
		// Assert
		BackgroundServiceOperations.Dispatch.ShouldBe("dispatch");
	}
}

/// <summary>
/// Unit tests for <see cref="BackgroundServiceResults"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class BackgroundServiceResultsShould : UnitTestBase
{
	[Fact]
	public void HaveSuccessConstant()
	{
		// Assert
		BackgroundServiceResults.Success.ShouldBe("success");
	}

	[Fact]
	public void HavePartialConstant()
	{
		// Assert
		BackgroundServiceResults.Partial.ShouldBe("partial");
	}

	[Fact]
	public void HaveEmptyConstant()
	{
		// Assert
		BackgroundServiceResults.Empty.ShouldBe("empty");
	}

	[Fact]
	public void HaveErrorConstant()
	{
		// Assert
		BackgroundServiceResults.Error.ShouldBe("error");
	}
}
