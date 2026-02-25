// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectBaseNamespace()
	{
		DispatchTelemetryConstants.BaseNamespace.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void HaveCorrectCoreName()
	{
		DispatchTelemetryConstants.CoreName.ShouldBe("Excalibur.Dispatch.Core");
	}

	[Fact]
	public void ActivitySources_HaveCorrectValues()
	{
		DispatchTelemetryConstants.ActivitySources.Core.ShouldBe("Excalibur.Dispatch.Core");
		DispatchTelemetryConstants.ActivitySources.Pipeline.ShouldBe("Excalibur.Dispatch.Pipeline");
		DispatchTelemetryConstants.ActivitySources.TimePolicy.ShouldBe("Excalibur.Dispatch.TimePolicy");
		DispatchTelemetryConstants.ActivitySources.BatchProcessor.ShouldBe("Excalibur.Dispatch.BatchProcessor");
		DispatchTelemetryConstants.ActivitySources.PoisonMessage.ShouldBe("Excalibur.Dispatch.PoisonMessage");
		DispatchTelemetryConstants.ActivitySources.PoisonMessageMiddleware.ShouldBe("Excalibur.Dispatch.PoisonMessage.Middleware");
		DispatchTelemetryConstants.ActivitySources.PoisonMessageCleanup.ShouldBe("Excalibur.Dispatch.PoisonMessage.Cleanup");
		DispatchTelemetryConstants.ActivitySources.AuditLoggingMiddleware.ShouldBe("Excalibur.Dispatch.AuditLoggingMiddleware");
		DispatchTelemetryConstants.ActivitySources.CircuitBreakerMiddleware.ShouldBe("Excalibur.Dispatch.CircuitBreakerMiddleware");
		DispatchTelemetryConstants.ActivitySources.RetryMiddleware.ShouldBe("Excalibur.Dispatch.RetryMiddleware");
		DispatchTelemetryConstants.ActivitySources.UnifiedBatchingMiddleware.ShouldBe("Excalibur.Dispatch.UnifiedBatchingMiddleware");
		DispatchTelemetryConstants.ActivitySources.ChannelTransport.ShouldBe("Excalibur.Dispatch.Transport.Common");
		DispatchTelemetryConstants.ActivitySources.OutboxBackgroundService.ShouldBe("Excalibur.Dispatch.Outbox.Publisher");
	}

	[Fact]
	public void Meters_HaveCorrectValues()
	{
		DispatchTelemetryConstants.Meters.Core.ShouldBe("Excalibur.Dispatch.Core");
		DispatchTelemetryConstants.Meters.Pipeline.ShouldBe("Excalibur.Dispatch.Pipeline");
		DispatchTelemetryConstants.Meters.TimePolicy.ShouldBe("Excalibur.Dispatch.TimePolicy");
		DispatchTelemetryConstants.Meters.BatchProcessor.ShouldBe("Excalibur.Dispatch.BatchProcessor");
		DispatchTelemetryConstants.Meters.Messaging.ShouldBe("Excalibur.Dispatch.Messaging");
		DispatchTelemetryConstants.Meters.ChannelTransport.ShouldBe("Excalibur.Dispatch.Transport.Common");
	}

	[Fact]
	public void Activities_HaveCorrectValues()
	{
		DispatchTelemetryConstants.Activities.ProcessMessage.ShouldBe("ProcessMessage");
		DispatchTelemetryConstants.Activities.StoreMessage.ShouldBe("StoreMessage");
		DispatchTelemetryConstants.Activities.GetMessages.ShouldBe("GetMessages");
		DispatchTelemetryConstants.Activities.StoreSchedule.ShouldBe("StoreSchedule");
		DispatchTelemetryConstants.Activities.GetSchedules.ShouldBe("GetSchedules");
		DispatchTelemetryConstants.Activities.CompleteSchedule.ShouldBe("CompleteSchedule");
		DispatchTelemetryConstants.Activities.CleanupCompleted.ShouldBe("CleanupCompleted");
		DispatchTelemetryConstants.Activities.CleanupFailed.ShouldBe("CleanupFailed");
		DispatchTelemetryConstants.Activities.BatchProcess.ShouldBe("BatchProcess");
		DispatchTelemetryConstants.Activities.BulkStore.ShouldBe("BulkStore");
	}

	[Fact]
	public void Tags_HaveCorrectValues()
	{
		DispatchTelemetryConstants.Tags.MessageId.ShouldBe("message.id");
		DispatchTelemetryConstants.Tags.MessageType.ShouldBe("message.type");
		DispatchTelemetryConstants.Tags.MessageName.ShouldBe("message.name");
		DispatchTelemetryConstants.Tags.MessageSize.ShouldBe("message.size");
		DispatchTelemetryConstants.Tags.MessageTenant.ShouldBe("message.tenant");
		DispatchTelemetryConstants.Tags.ScheduleId.ShouldBe("schedule.id");
		DispatchTelemetryConstants.Tags.ScheduleEnabled.ShouldBe("schedule.enabled");
		DispatchTelemetryConstants.Tags.ScheduleType.ShouldBe("schedule.type");
		DispatchTelemetryConstants.Tags.ScheduleCount.ShouldBe("schedule.count");
		DispatchTelemetryConstants.Tags.OperationType.ShouldBe("operation.type");
		DispatchTelemetryConstants.Tags.OperationResult.ShouldBe("operation.result");
		DispatchTelemetryConstants.Tags.OperationStage.ShouldBe("operation.stage");
		DispatchTelemetryConstants.Tags.StoreType.ShouldBe("store.type");
		DispatchTelemetryConstants.Tags.StoreName.ShouldBe("store.name");
		DispatchTelemetryConstants.Tags.CacheHit.ShouldBe("cache.hit");
		DispatchTelemetryConstants.Tags.BatchSize.ShouldBe("batch.size");
		DispatchTelemetryConstants.Tags.IsDuplicate.ShouldBe("message.is_duplicate");
		DispatchTelemetryConstants.Tags.ErrorType.ShouldBe("error.type");
		DispatchTelemetryConstants.Tags.ErrorMessage.ShouldBe("error.message");
		DispatchTelemetryConstants.Tags.IsRetryable.ShouldBe("error.retryable");
	}

	[Fact]
	public void TagValues_HaveCorrectValues()
	{
		DispatchTelemetryConstants.TagValues.Success.ShouldBe("success");
		DispatchTelemetryConstants.TagValues.Failure.ShouldBe("failure");
		DispatchTelemetryConstants.TagValues.Timeout.ShouldBe("timeout");
		DispatchTelemetryConstants.TagValues.Cancelled.ShouldBe("cancelled");
		DispatchTelemetryConstants.TagValues.InboxStore.ShouldBe("inbox");
		DispatchTelemetryConstants.TagValues.OutboxStore.ShouldBe("outbox");
		DispatchTelemetryConstants.TagValues.ScheduleStore.ShouldBe("schedule");
		DispatchTelemetryConstants.TagValues.Store.ShouldBe("store");
		DispatchTelemetryConstants.TagValues.Retrieve.ShouldBe("retrieve");
		DispatchTelemetryConstants.TagValues.Update.ShouldBe("update");
		DispatchTelemetryConstants.TagValues.Delete.ShouldBe("delete");
		DispatchTelemetryConstants.TagValues.Cleanup.ShouldBe("cleanup");
		DispatchTelemetryConstants.TagValues.Hit.ShouldBe("hit");
		DispatchTelemetryConstants.TagValues.Miss.ShouldBe("miss");
		DispatchTelemetryConstants.TagValues.Evicted.ShouldBe("evicted");
	}

	[Fact]
	public void CoreActivitySource_EqualsCoreMeter()
	{
		// Both should reference the same CoreName constant
		DispatchTelemetryConstants.ActivitySources.Core
			.ShouldBe(DispatchTelemetryConstants.Meters.Core);
	}

	[Fact]
	public void AllActivitySources_StartWithBaseNamespace()
	{
		var sources = new[]
		{
			DispatchTelemetryConstants.ActivitySources.Core,
			DispatchTelemetryConstants.ActivitySources.Pipeline,
			DispatchTelemetryConstants.ActivitySources.TimePolicy,
			DispatchTelemetryConstants.ActivitySources.BatchProcessor,
			DispatchTelemetryConstants.ActivitySources.PoisonMessage,
			DispatchTelemetryConstants.ActivitySources.PoisonMessageMiddleware,
			DispatchTelemetryConstants.ActivitySources.PoisonMessageCleanup,
			DispatchTelemetryConstants.ActivitySources.AuditLoggingMiddleware,
			DispatchTelemetryConstants.ActivitySources.CircuitBreakerMiddleware,
			DispatchTelemetryConstants.ActivitySources.RetryMiddleware,
			DispatchTelemetryConstants.ActivitySources.UnifiedBatchingMiddleware,
		};

		foreach (var source in sources)
		{
			source.ShouldStartWith(DispatchTelemetryConstants.BaseNamespace);
		}
	}
}
