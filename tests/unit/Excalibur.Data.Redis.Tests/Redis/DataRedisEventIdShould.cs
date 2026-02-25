// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis.Diagnostics;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="DataRedisEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.Redis")]
[Trait("Priority", "0")]
public sealed class DataRedisEventIdShould : UnitTestBase
{
	#region Connection/Initialization Event ID Tests (107000-107099)

	[Fact]
	public void HaveInitializingInConnectionRange()
	{
		DataRedisEventId.Initializing.ShouldBe(107000);
	}

	[Fact]
	public void HaveAllConnectionEventIdsInExpectedRange()
	{
		DataRedisEventId.Initializing.ShouldBeInRange(107000, 107099);
		DataRedisEventId.ConnectionTestSuccessful.ShouldBeInRange(107000, 107099);
		DataRedisEventId.ConnectionTestFailed.ShouldBeInRange(107000, 107099);
		DataRedisEventId.Disposing.ShouldBeInRange(107000, 107099);
		DataRedisEventId.DisposeError.ShouldBeInRange(107000, 107099);
		DataRedisEventId.MetadataRetrievalFailed.ShouldBeInRange(107000, 107099);
		DataRedisEventId.PoolStatsFailed.ShouldBeInRange(107000, 107099);
	}

	#endregion

	#region Data Request Execution Event ID Tests (107100-107199)

	[Fact]
	public void HaveExecutingRequestInExecutionRange()
	{
		DataRedisEventId.ExecutingRequest.ShouldBe(107100);
	}

	[Fact]
	public void HaveAllExecutionEventIdsInExpectedRange()
	{
		DataRedisEventId.ExecutingRequest.ShouldBeInRange(107100, 107199);
		DataRedisEventId.ExecutionFailed.ShouldBeInRange(107100, 107199);
		DataRedisEventId.ExecutingRequestInTransaction.ShouldBeInRange(107100, 107199);
		DataRedisEventId.TransactionExecutionFailed.ShouldBeInRange(107100, 107199);
	}

	#endregion

	#region Retry Policy Event ID Tests (107200-107299)

	[Fact]
	public void HaveRetryWarningInRetryRange()
	{
		DataRedisEventId.RetryWarning.ShouldBe(107200);
	}

	[Fact]
	public void HaveAllRetryEventIdsInExpectedRange()
	{
		DataRedisEventId.RetryWarning.ShouldBeInRange(107200, 107299);
		DataRedisEventId.DocumentRetryWarning.ShouldBeInRange(107200, 107299);
		DataRedisEventId.ProviderRetryWarning.ShouldBeInRange(107200, 107299);
	}

	#endregion

	#region Outbox Store Event ID Tests (107300-107399)

	[Fact]
	public void HaveOutboxMessageStagedInOutboxRange()
	{
		DataRedisEventId.OutboxMessageStaged.ShouldBe(107300);
	}

	[Fact]
	public void HaveAllOutboxEventIdsInExpectedRange()
	{
		DataRedisEventId.OutboxMessageStaged.ShouldBeInRange(107300, 107399);
		DataRedisEventId.OutboxMessageEnqueued.ShouldBeInRange(107300, 107399);
		DataRedisEventId.OutboxMessageSent.ShouldBeInRange(107300, 107399);
		DataRedisEventId.OutboxMessageFailed.ShouldBeInRange(107300, 107399);
		DataRedisEventId.OutboxCleanedUp.ShouldBeInRange(107300, 107399);
	}

	#endregion

	#region Inbox Store Event ID Tests (107400-107499)

	[Fact]
	public void HaveInboxEntryCreatedInInboxRange()
	{
		DataRedisEventId.InboxEntryCreated.ShouldBe(107400);
	}

	[Fact]
	public void HaveAllInboxEventIdsInExpectedRange()
	{
		DataRedisEventId.InboxEntryCreated.ShouldBeInRange(107400, 107499);
		DataRedisEventId.InboxEntryProcessed.ShouldBeInRange(107400, 107499);
		DataRedisEventId.InboxTryMarkProcessedSuccess.ShouldBeInRange(107400, 107499);
		DataRedisEventId.InboxTryMarkProcessedDuplicate.ShouldBeInRange(107400, 107499);
		DataRedisEventId.InboxEntryFailed.ShouldBeInRange(107400, 107499);
		DataRedisEventId.InboxCleanedUp.ShouldBeInRange(107400, 107499);
	}

	#endregion

	#region Redis Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInRedisReservedRange()
	{
		// Redis reserved range is 107000-107999
		var allEventIds = GetAllRedisEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(107000, 107999,
				$"Event ID {eventId} is outside Redis reserved range (107000-107999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllRedisEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllRedisEventIds();
		allEventIds.Length.ShouldBeGreaterThan(20);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllRedisEventIds()
	{
		return
		[
			// Connection/Initialization (107000-107099)
			DataRedisEventId.Initializing,
			DataRedisEventId.ConnectionTestSuccessful,
			DataRedisEventId.ConnectionTestFailed,
			DataRedisEventId.Disposing,
			DataRedisEventId.DisposeError,
			DataRedisEventId.MetadataRetrievalFailed,
			DataRedisEventId.PoolStatsFailed,

			// Data Request Execution (107100-107199)
			DataRedisEventId.ExecutingRequest,
			DataRedisEventId.ExecutionFailed,
			DataRedisEventId.ExecutingRequestInTransaction,
			DataRedisEventId.TransactionExecutionFailed,

			// Retry Policy (107200-107299)
			DataRedisEventId.RetryWarning,
			DataRedisEventId.DocumentRetryWarning,
			DataRedisEventId.ProviderRetryWarning,

			// Outbox Store (107300-107399)
			DataRedisEventId.OutboxMessageStaged,
			DataRedisEventId.OutboxMessageEnqueued,
			DataRedisEventId.OutboxMessageSent,
			DataRedisEventId.OutboxMessageFailed,
			DataRedisEventId.OutboxCleanedUp,

			// Inbox Store (107400-107499)
			DataRedisEventId.InboxEntryCreated,
			DataRedisEventId.InboxEntryProcessed,
			DataRedisEventId.InboxTryMarkProcessedSuccess,
			DataRedisEventId.InboxTryMarkProcessedDuplicate,
			DataRedisEventId.InboxEntryFailed,
			DataRedisEventId.InboxCleanedUp
		];
	}

	#endregion
}
