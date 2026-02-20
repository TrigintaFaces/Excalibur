// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;

namespace Excalibur.Tests.Cdc.Diagnostics;

/// <summary>
/// Unit tests for <see cref="DataProcessingEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.DataProcessing")]
[Trait("Priority", "0")]
public sealed class DataProcessingEventIdShould : UnitTestBase
{
	#region Data Orchestration Manager Event ID Tests (107000-107099)

	[Fact]
	public void HaveProcessorNotFoundInOrchestrationRange()
	{
		DataProcessingEventId.ProcessorNotFound.ShouldBe(107000);
	}

	[Fact]
	public void HaveAllOrchestrationManagerEventIdsInExpectedRange()
	{
		DataProcessingEventId.ProcessorNotFound.ShouldBeInRange(107000, 107099);
		DataProcessingEventId.ProcessingDataTaskError.ShouldBeInRange(107000, 107099);
		DataProcessingEventId.UpdateCompletedCountMismatch.ShouldBeInRange(107000, 107099);
	}

	#endregion

	#region Data Processor Core Event ID Tests (107100-107199)

	[Fact]
	public void HaveDisposeAsyncInProcessorCoreRange()
	{
		DataProcessingEventId.DisposeAsync.ShouldBe(107100);
	}

	[Fact]
	public void HaveAllProcessorCoreEventIdsInExpectedRange()
	{
		DataProcessingEventId.DisposeAsync.ShouldBeInRange(107100, 107199);
		DataProcessingEventId.ConsumerNotCompletedAsync.ShouldBeInRange(107100, 107199);
		DataProcessingEventId.ConsumerTimeoutAsync.ShouldBeInRange(107100, 107199);
		DataProcessingEventId.DisposeAsyncError.ShouldBeInRange(107100, 107199);
		DataProcessingEventId.DisposeSync.ShouldBeInRange(107100, 107199);
		DataProcessingEventId.ConsumerNotCompletedSync.ShouldBeInRange(107100, 107199);
		DataProcessingEventId.ConsumerTimeoutSync.ShouldBeInRange(107100, 107199);
		DataProcessingEventId.DisposeError.ShouldBeInRange(107100, 107199);
	}

	#endregion

	#region Data Processor Consumer Event ID Tests (107200-107299)

	[Fact]
	public void HaveConsumerDisposalRequestedInConsumerRange()
	{
		DataProcessingEventId.ConsumerDisposalRequested.ShouldBe(107200);
	}

	[Fact]
	public void HaveAllConsumerEventIdsInExpectedRange()
	{
		DataProcessingEventId.ConsumerDisposalRequested.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.NoMoreRecordsConsumerExit.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.QueueEmptyWaiting.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.ProcessingBatch.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.ProcessingRecordError.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.CompletedBatch.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.CompletedProcessing.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.ConsumerCanceled.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.ConsumerError.ShouldBeInRange(107200, 107299);
		DataProcessingEventId.NoHandlerFound.ShouldBeInRange(107200, 107299);
	}

	#endregion

	#region Data Processor Producer Event ID Tests (107300-107399)

	[Fact]
	public void HaveNoMoreRecordsProducerExitInProducerRange()
	{
		DataProcessingEventId.NoMoreRecordsProducerExit.ShouldBe(107300);
	}

	[Fact]
	public void HaveAllProducerEventIdsInExpectedRange()
	{
		DataProcessingEventId.NoMoreRecordsProducerExit.ShouldBeInRange(107300, 107399);
		DataProcessingEventId.EnqueuingRecords.ShouldBeInRange(107300, 107399);
		DataProcessingEventId.SuccessfullyEnqueued.ShouldBeInRange(107300, 107399);
		DataProcessingEventId.ProducerCanceled.ShouldBeInRange(107300, 107399);
		DataProcessingEventId.ProducerError.ShouldBeInRange(107300, 107399);
		DataProcessingEventId.ProducerCompleted.ShouldBeInRange(107300, 107399);
		DataProcessingEventId.ApplicationStopping.ShouldBeInRange(107300, 107399);
		DataProcessingEventId.ProducerCancellationRequested.ShouldBeInRange(107300, 107399);
		DataProcessingEventId.WaitingForConsumer.ShouldBeInRange(107300, 107399);
	}

	#endregion

	#region Data Processing Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInDataProcessingReservedRange()
	{
		// Data Processing reserved range is 107000-107999
		var allEventIds = GetAllDataProcessingEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(107000, 107999,
				$"Event ID {eventId} is outside Data Processing reserved range (107000-107999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllDataProcessingEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllDataProcessingEventIds();
		allEventIds.Length.ShouldBeGreaterThan(25);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllDataProcessingEventIds()
	{
		return
		[
			// Data Orchestration Manager (107000-107099)
			DataProcessingEventId.ProcessorNotFound,
			DataProcessingEventId.ProcessingDataTaskError,
			DataProcessingEventId.UpdateCompletedCountMismatch,

			// Data Processor Core (107100-107199)
			DataProcessingEventId.DisposeAsync,
			DataProcessingEventId.ConsumerNotCompletedAsync,
			DataProcessingEventId.ConsumerTimeoutAsync,
			DataProcessingEventId.DisposeAsyncError,
			DataProcessingEventId.DisposeSync,
			DataProcessingEventId.ConsumerNotCompletedSync,
			DataProcessingEventId.ConsumerTimeoutSync,
			DataProcessingEventId.DisposeError,

			// Data Processor Consumer (107200-107299)
			DataProcessingEventId.ConsumerDisposalRequested,
			DataProcessingEventId.NoMoreRecordsConsumerExit,
			DataProcessingEventId.QueueEmptyWaiting,
			DataProcessingEventId.ProcessingBatch,
			DataProcessingEventId.ProcessingRecordError,
			DataProcessingEventId.CompletedBatch,
			DataProcessingEventId.CompletedProcessing,
			DataProcessingEventId.ConsumerCanceled,
			DataProcessingEventId.ConsumerError,
			DataProcessingEventId.NoHandlerFound,

			// Data Processor Producer (107300-107399)
			DataProcessingEventId.NoMoreRecordsProducerExit,
			DataProcessingEventId.EnqueuingRecords,
			DataProcessingEventId.SuccessfullyEnqueued,
			DataProcessingEventId.ProducerCanceled,
			DataProcessingEventId.ProducerError,
			DataProcessingEventId.ProducerCompleted,
			DataProcessingEventId.ApplicationStopping,
			DataProcessingEventId.ProducerCancellationRequested,
			DataProcessingEventId.WaitingForConsumer
		];
	}

	#endregion
}
