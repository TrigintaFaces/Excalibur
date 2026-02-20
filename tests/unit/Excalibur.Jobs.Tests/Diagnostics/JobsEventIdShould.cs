// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

namespace Excalibur.Jobs.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="JobsEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Priority", "0")]
public sealed class JobsEventIdShould : UnitTestBase
{
	#region Azure Logic Apps Event IDs (146100-146199)

	[Fact]
	public void HaveAzureLogicAppsWorkflowCreatedInAzureLogicAppsRange()
	{
		JobsEventId.AzureLogicAppsWorkflowCreated.ShouldBe(146100);
	}

	[Fact]
	public void HaveAzureLogicAppsWorkflowCreationFailedInAzureLogicAppsRange()
	{
		JobsEventId.AzureLogicAppsWorkflowCreationFailed.ShouldBe(146101);
	}

	[Fact]
	public void HaveAzureLogicAppsWorkflowDeletedInAzureLogicAppsRange()
	{
		JobsEventId.AzureLogicAppsWorkflowDeleted.ShouldBe(146102);
	}

	[Fact]
	public void HaveAzureLogicAppsWorkflowNotFoundInAzureLogicAppsRange()
	{
		JobsEventId.AzureLogicAppsWorkflowNotFound.ShouldBe(146103);
	}

	[Fact]
	public void HaveAzureLogicAppsWorkflowDeletionFailedInAzureLogicAppsRange()
	{
		JobsEventId.AzureLogicAppsWorkflowDeletionFailed.ShouldBe(146104);
	}

	#endregion

	#region AWS EventBridge Scheduler Event IDs (146200-146299)

	[Fact]
	public void HaveAwsSchedulerScheduleCreatedInAwsSchedulerRange()
	{
		JobsEventId.AwsSchedulerScheduleCreated.ShouldBe(146200);
	}

	[Fact]
	public void HaveAwsSchedulerScheduleCreationFailedInAwsSchedulerRange()
	{
		JobsEventId.AwsSchedulerScheduleCreationFailed.ShouldBe(146201);
	}

	[Fact]
	public void HaveAwsSchedulerScheduleDeletedInAwsSchedulerRange()
	{
		JobsEventId.AwsSchedulerScheduleDeleted.ShouldBe(146202);
	}

	[Fact]
	public void HaveAwsSchedulerScheduleNotFoundInAwsSchedulerRange()
	{
		JobsEventId.AwsSchedulerScheduleNotFound.ShouldBe(146203);
	}

	[Fact]
	public void HaveAwsSchedulerScheduleDeletionFailedInAwsSchedulerRange()
	{
		JobsEventId.AwsSchedulerScheduleDeletionFailed.ShouldBe(146204);
	}

	#endregion

	#region Job Config Watcher Event IDs (147100-147199)

	[Fact]
	public void HaveStartingJobWatcherServiceInWatcherRange()
	{
		JobsEventId.StartingJobWatcherService.ShouldBe(147100);
	}

	[Fact]
	public void HaveInitialConfigurationLoadedInWatcherRange()
	{
		JobsEventId.InitialConfigurationLoaded.ShouldBe(147101);
	}

	[Fact]
	public void HaveConfigurationChangeDetectedInWatcherRange()
	{
		JobsEventId.ConfigurationChangeDetected.ShouldBe(147102);
	}

	[Fact]
	public void HaveErrorHandlingConfigurationChangeInWatcherRange()
	{
		JobsEventId.ErrorHandlingConfigurationChange.ShouldBe(147103);
	}

	[Fact]
	public void HaveJobWatcherServiceStartedSuccessfullyInWatcherRange()
	{
		JobsEventId.JobWatcherServiceStartedSuccessfully.ShouldBe(147104);
	}

	[Fact]
	public void HaveErrorStartingJobWatcherServiceInWatcherRange()
	{
		JobsEventId.ErrorStartingJobWatcherService.ShouldBe(147105);
	}

	[Fact]
	public void HaveStoppingJobWatcherServiceInWatcherRange()
	{
		JobsEventId.StoppingJobWatcherService.ShouldBe(147106);
	}

	[Fact]
	public void HaveJobWatcherServiceStoppedSuccessfullyInWatcherRange()
	{
		JobsEventId.JobWatcherServiceStoppedSuccessfully.ShouldBe(147107);
	}

	[Fact]
	public void HaveErrorStoppingJobWatcherServiceInWatcherRange()
	{
		JobsEventId.ErrorStoppingJobWatcherService.ShouldBe(147108);
	}

	[Fact]
	public void HavePausingJobInWatcherRange()
	{
		JobsEventId.PausingJob.ShouldBe(147109);
	}

	[Fact]
	public void HaveResumingJobInWatcherRange()
	{
		JobsEventId.ResumingJob.ShouldBe(147110);
	}

	[Fact]
	public void HaveErrorCreatingJobConfigWatcherServiceInWatcherRange()
	{
		JobsEventId.ErrorCreatingJobConfigWatcherService.ShouldBe(147111);
	}

	#endregion

	#region CDC Job Event IDs (147200-147249)

	[Fact]
	public void HaveCdcJobStartingInCdcJobRange()
	{
		JobsEventId.CdcJobStarting.ShouldBe(147200);
	}

	[Fact]
	public void HaveCdcJobCompletedInCdcJobRange()
	{
		JobsEventId.CdcJobCompleted.ShouldBe(147201);
	}

	[Fact]
	public void HaveCdcJobErrorInCdcJobRange()
	{
		JobsEventId.CdcJobError.ShouldBe(147202);
	}

	[Fact]
	public void HaveCdcProcessingErrorInCdcJobRange()
	{
		JobsEventId.CdcProcessingError.ShouldBe(147203);
	}

	#endregion

	#region Data Processing Job Event IDs (147250-147299)

	[Fact]
	public void HaveDataProcessingJobStartingInDataProcessingRange()
	{
		JobsEventId.DataProcessingJobStarting.ShouldBe(147250);
	}

	[Fact]
	public void HaveDataProcessingJobCompletedInDataProcessingRange()
	{
		JobsEventId.DataProcessingJobCompleted.ShouldBe(147251);
	}

	[Fact]
	public void HaveDataProcessingJobErrorInDataProcessingRange()
	{
		JobsEventId.DataProcessingJobError.ShouldBe(147252);
	}

	#endregion

	#region Quartz Job Adapter Event IDs (147300-147399)

	[Fact]
	public void HaveJobTypeNotFoundOrInvalidInQuartzRange()
	{
		JobsEventId.JobTypeNotFoundOrInvalid.ShouldBe(147300);
	}

	[Fact]
	public void HaveCouldNotResolveJobTypeInQuartzRange()
	{
		JobsEventId.CouldNotResolveJobType.ShouldBe(147301);
	}

	[Fact]
	public void HaveExecutingJobInQuartzRange()
	{
		JobsEventId.ExecutingJob.ShouldBe(147302);
	}

	[Fact]
	public void HaveJobDoesNotImplementInterfaceInQuartzRange()
	{
		JobsEventId.JobDoesNotImplementInterface.ShouldBe(147303);
	}

	[Fact]
	public void HaveJobCompletedSuccessfullyInQuartzRange()
	{
		JobsEventId.JobCompletedSuccessfully.ShouldBe(147304);
	}

	[Fact]
	public void HaveErrorExecutingJobInQuartzRange()
	{
		JobsEventId.ErrorExecutingJob.ShouldBe(147305);
	}

	#endregion

	#region Redis Job Coordinator Event IDs (147400-147409)

	[Fact]
	public void HaveRedisLockAcquiredInRedisRange()
	{
		JobsEventId.RedisLockAcquired.ShouldBe(147400);
	}

	[Fact]
	public void HaveRedisLockAcquisitionFailedInRedisRange()
	{
		JobsEventId.RedisLockAcquisitionFailed.ShouldBe(147401);
	}

	[Fact]
	public void HaveRedisInstanceRegisteredInRedisRange()
	{
		JobsEventId.RedisInstanceRegistered.ShouldBe(147402);
	}

	[Fact]
	public void HaveRedisInstanceUnregisteredInRedisRange()
	{
		JobsEventId.RedisInstanceUnregistered.ShouldBe(147403);
	}

	[Fact]
	public void HaveRedisInstanceDeserializationFailedInRedisRange()
	{
		JobsEventId.RedisInstanceDeserializationFailed.ShouldBe(147404);
	}

	[Fact]
	public void HaveRedisLeaderElectedInRedisRange()
	{
		JobsEventId.RedisLeaderElected.ShouldBe(147405);
	}

	[Fact]
	public void HaveRedisLeaderDeserializationFailedInRedisRange()
	{
		JobsEventId.RedisLeaderDeserializationFailed.ShouldBe(147406);
	}

	[Fact]
	public void HaveRedisJobDistributedInRedisRange()
	{
		JobsEventId.RedisJobDistributed.ShouldBe(147407);
	}

	[Fact]
	public void HaveRedisNoInstanceAvailableInRedisRange()
	{
		JobsEventId.RedisNoInstanceAvailable.ShouldBe(147408);
	}

	[Fact]
	public void HaveRedisJobCompletionReportedInRedisRange()
	{
		JobsEventId.RedisJobCompletionReported.ShouldBe(147409);
	}

	#endregion

	#region Health Check Job Event IDs (147500-147509)

	[Fact]
	public void HaveHealthCheckJobStartingInHealthCheckRange()
	{
		JobsEventId.HealthCheckJobStarting.ShouldBe(147500);
	}

	[Fact]
	public void HaveHealthCheckJobCompletedInHealthCheckRange()
	{
		JobsEventId.HealthCheckJobCompleted.ShouldBe(147501);
	}

	[Fact]
	public void HaveHealthCheckWarningInHealthCheckRange()
	{
		JobsEventId.HealthCheckWarning.ShouldBe(147502);
	}

	[Fact]
	public void HaveHealthCheckErrorInHealthCheckRange()
	{
		JobsEventId.HealthCheckError.ShouldBe(147503);
	}

	[Fact]
	public void HaveHealthCheckDataInHealthCheckRange()
	{
		JobsEventId.HealthCheckData.ShouldBe(147504);
	}

	[Fact]
	public void HaveHealthCheckJobFailedInHealthCheckRange()
	{
		JobsEventId.HealthCheckJobFailed.ShouldBe(147505);
	}

	#endregion

	#region Outbox Processor Job Event IDs (147510-147519)

	[Fact]
	public void HaveOutboxProcessorJobStartingInOutboxProcessorRange()
	{
		JobsEventId.OutboxProcessorJobStarting.ShouldBe(147510);
	}

	[Fact]
	public void HaveOutboxProcessorOutboxMissingInOutboxProcessorRange()
	{
		JobsEventId.OutboxProcessorOutboxMissing.ShouldBe(147511);
	}

	[Fact]
	public void HaveOutboxProcessorJobCompletedInOutboxProcessorRange()
	{
		JobsEventId.OutboxProcessorJobCompleted.ShouldBe(147512);
	}

	[Fact]
	public void HaveOutboxProcessorNoMessagesInOutboxProcessorRange()
	{
		JobsEventId.OutboxProcessorNoMessages.ShouldBe(147513);
	}

	[Fact]
	public void HaveOutboxProcessorJobFailedInOutboxProcessorRange()
	{
		JobsEventId.OutboxProcessorJobFailed.ShouldBe(147514);
	}

	#endregion

	#region Outbox Job Event IDs (147520-147529)

	[Fact]
	public void HaveOutboxJobExecutionStartingInOutboxJobRange()
	{
		JobsEventId.OutboxJobExecutionStarting.ShouldBe(147520);
	}

	[Fact]
	public void HaveOutboxJobExecutionCompletedInOutboxJobRange()
	{
		JobsEventId.OutboxJobExecutionCompleted.ShouldBe(147521);
	}

	[Fact]
	public void HaveOutboxJobExecutionFailedInOutboxJobRange()
	{
		JobsEventId.OutboxJobExecutionFailed.ShouldBe(147522);
	}

	#endregion

	#region Quartz Generic Job Adapter Event IDs (147600-147609)

	[Fact]
	public void HaveGenericContextDeserializationFailedInGenericJobRange()
	{
		JobsEventId.GenericContextDeserializationFailed.ShouldBe(147600);
	}

	[Fact]
	public void HaveGenericContextNotFoundOrInvalidInGenericJobRange()
	{
		JobsEventId.GenericContextNotFoundOrInvalid.ShouldBe(147601);
	}

	[Fact]
	public void HaveExecutingGenericJobInGenericJobRange()
	{
		JobsEventId.ExecutingGenericJob.ShouldBe(147602);
	}

	[Fact]
	public void HaveGenericJobCompletedSuccessfullyInGenericJobRange()
	{
		JobsEventId.GenericJobCompletedSuccessfully.ShouldBe(147603);
	}

	[Fact]
	public void HaveGenericJobExecutionFailedInGenericJobRange()
	{
		JobsEventId.GenericJobExecutionFailed.ShouldBe(147604);
	}

	#endregion

	#region Workflow Job Event IDs (147700-147709)

	[Fact]
	public void HaveWorkflowJobStartingInWorkflowJobRange()
	{
		JobsEventId.WorkflowJobStarting.ShouldBe(147700);
	}

	[Fact]
	public void HaveWorkflowJobCompletedInWorkflowJobRange()
	{
		JobsEventId.WorkflowJobCompleted.ShouldBe(147701);
	}

	[Fact]
	public void HaveWorkflowJobFailedInWorkflowJobRange()
	{
		JobsEventId.WorkflowJobFailed.ShouldBe(147702);
	}

	[Fact]
	public void HaveWorkflowJobUnhandledExceptionInWorkflowJobRange()
	{
		JobsEventId.WorkflowJobUnhandledException.ShouldBe(147703);
	}

	#endregion

	#region Overall Range Validation

	[Fact]
	public void HaveAllEventIdsWithinJobsPackageRange()
	{
		// Jobs package owns range 140000-147999
		var allEventIds = GetAllEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(140000, 147999,
				$"Event ID {eventId} is outside Jobs package range (140000-147999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIdsForAllDefinedEventIds()
	{
		var allEventIds = GetAllEventIds();

		allEventIds.Distinct().Count().ShouldBe(
			allEventIds.Length,
			"All event IDs must be unique");
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllEventIds()
	{
		return
		[
			// Azure Logic Apps (146100-146199)
			JobsEventId.AzureLogicAppsWorkflowCreated, JobsEventId.AzureLogicAppsWorkflowCreationFailed,
			JobsEventId.AzureLogicAppsWorkflowDeleted, JobsEventId.AzureLogicAppsWorkflowNotFound,
			JobsEventId.AzureLogicAppsWorkflowDeletionFailed,

			// AWS EventBridge (146200-146299)
			JobsEventId.AwsSchedulerScheduleCreated, JobsEventId.AwsSchedulerScheduleCreationFailed,
			JobsEventId.AwsSchedulerScheduleDeleted, JobsEventId.AwsSchedulerScheduleNotFound,
			JobsEventId.AwsSchedulerScheduleDeletionFailed,

			// Config Watcher (147100-147199)
			JobsEventId.StartingJobWatcherService, JobsEventId.InitialConfigurationLoaded,
			JobsEventId.ConfigurationChangeDetected, JobsEventId.ErrorHandlingConfigurationChange,
			JobsEventId.JobWatcherServiceStartedSuccessfully, JobsEventId.ErrorStartingJobWatcherService,
			JobsEventId.StoppingJobWatcherService, JobsEventId.JobWatcherServiceStoppedSuccessfully,
			JobsEventId.ErrorStoppingJobWatcherService, JobsEventId.PausingJob, JobsEventId.ResumingJob,
			JobsEventId.ErrorCreatingJobConfigWatcherService,

			// CDC Job (147200-147249)
			JobsEventId.CdcJobStarting, JobsEventId.CdcJobCompleted,
			JobsEventId.CdcJobError, JobsEventId.CdcProcessingError,

			// Data Processing (147250-147299)
			JobsEventId.DataProcessingJobStarting, JobsEventId.DataProcessingJobCompleted,
			JobsEventId.DataProcessingJobError,

			// Quartz Adapter (147300-147399)
			JobsEventId.JobTypeNotFoundOrInvalid, JobsEventId.CouldNotResolveJobType,
			JobsEventId.ExecutingJob, JobsEventId.JobDoesNotImplementInterface,
			JobsEventId.JobCompletedSuccessfully, JobsEventId.ErrorExecutingJob,

			// Redis Coordinator (147400-147409)
			JobsEventId.RedisLockAcquired, JobsEventId.RedisLockAcquisitionFailed,
			JobsEventId.RedisInstanceRegistered, JobsEventId.RedisInstanceUnregistered,
			JobsEventId.RedisInstanceDeserializationFailed, JobsEventId.RedisLeaderElected,
			JobsEventId.RedisLeaderDeserializationFailed, JobsEventId.RedisJobDistributed,
			JobsEventId.RedisNoInstanceAvailable, JobsEventId.RedisJobCompletionReported,

			// Health Check (147500-147509)
			JobsEventId.HealthCheckJobStarting, JobsEventId.HealthCheckJobCompleted,
			JobsEventId.HealthCheckWarning, JobsEventId.HealthCheckError,
			JobsEventId.HealthCheckData, JobsEventId.HealthCheckJobFailed,

			// Outbox Processor (147510-147519)
			JobsEventId.OutboxProcessorJobStarting, JobsEventId.OutboxProcessorOutboxMissing,
			JobsEventId.OutboxProcessorJobCompleted, JobsEventId.OutboxProcessorNoMessages,
			JobsEventId.OutboxProcessorJobFailed,

			// Outbox Job (147520-147529)
			JobsEventId.OutboxJobExecutionStarting, JobsEventId.OutboxJobExecutionCompleted,
			JobsEventId.OutboxJobExecutionFailed,

			// Generic Job (147600-147609)
			JobsEventId.GenericContextDeserializationFailed, JobsEventId.GenericContextNotFoundOrInvalid,
			JobsEventId.ExecutingGenericJob, JobsEventId.GenericJobCompletedSuccessfully,
			JobsEventId.GenericJobExecutionFailed,

			// Workflow Job (147700-147709)
			JobsEventId.WorkflowJobStarting, JobsEventId.WorkflowJobCompleted,
			JobsEventId.WorkflowJobFailed, JobsEventId.WorkflowJobUnhandledException
		];
	}

	#endregion
}
