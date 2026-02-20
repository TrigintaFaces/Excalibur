// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Jobs.Diagnostics;

/// <summary>
/// Event IDs for job scheduling infrastructure (140000-147999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>146100-146199: Azure Logic Apps Job Provider</item>
/// <item>146200-146299: AWS EventBridge Scheduler Job Provider</item>
/// <item>147100-147199: Job Config Watcher</item>
/// <item>147200-147249: CDC Job</item>
/// <item>147250-147299: Data Processing Job</item>
/// <item>147300-147399: Quartz Job Adapter</item>
/// <item>147400-147409: Redis Job Coordinator</item>
/// <item>147410-147429: SQL Server Job Coordinator</item>
/// <item>147500-147509: Health Check Job</item>
/// <item>147510-147519: Outbox Processor Job</item>
/// <item>147520-147529: Outbox Job</item>
/// <item>147600-147609: Quartz Generic Job Adapter</item>
/// <item>147700-147709: Workflow Job</item>
/// </list>
/// </remarks>
public static class JobsEventId
{
	// ========================================
	// 146100-146199: Azure Logic Apps Job Provider
	// ========================================

	/// <summary>Azure Logic Apps workflow created successfully.</summary>
	public const int AzureLogicAppsWorkflowCreated = 146100;

	/// <summary>Azure Logic Apps workflow creation failed.</summary>
	public const int AzureLogicAppsWorkflowCreationFailed = 146101;

	/// <summary>Azure Logic Apps workflow deleted successfully.</summary>
	public const int AzureLogicAppsWorkflowDeleted = 146102;

	/// <summary>Azure Logic Apps workflow not found for deletion.</summary>
	public const int AzureLogicAppsWorkflowNotFound = 146103;

	/// <summary>Azure Logic Apps workflow deletion failed.</summary>
	public const int AzureLogicAppsWorkflowDeletionFailed = 146104;

	// ========================================
	// 146200-146299: AWS EventBridge Scheduler Job Provider
	// ========================================

	/// <summary>AWS EventBridge schedule created successfully.</summary>
	public const int AwsSchedulerScheduleCreated = 146200;

	/// <summary>AWS EventBridge schedule creation failed.</summary>
	public const int AwsSchedulerScheduleCreationFailed = 146201;

	/// <summary>AWS EventBridge schedule deleted successfully.</summary>
	public const int AwsSchedulerScheduleDeleted = 146202;

	/// <summary>AWS EventBridge schedule not found for deletion.</summary>
	public const int AwsSchedulerScheduleNotFound = 146203;

	/// <summary>AWS EventBridge schedule deletion failed.</summary>
	public const int AwsSchedulerScheduleDeletionFailed = 146204;

	// ========================================
	// 147100-147199: Job Config Watcher
	// ========================================

	/// <summary>Starting job watcher service.</summary>
	public const int StartingJobWatcherService = 147100;

	/// <summary>Initial configuration loaded.</summary>
	public const int InitialConfigurationLoaded = 147101;

	/// <summary>Configuration change detected.</summary>
	public const int ConfigurationChangeDetected = 147102;

	/// <summary>Error handling configuration change.</summary>
	public const int ErrorHandlingConfigurationChange = 147103;

	/// <summary>Job watcher service started successfully.</summary>
	public const int JobWatcherServiceStartedSuccessfully = 147104;

	/// <summary>Error starting job watcher service.</summary>
	public const int ErrorStartingJobWatcherService = 147105;

	/// <summary>Stopping job watcher service.</summary>
	public const int StoppingJobWatcherService = 147106;

	/// <summary>Job watcher service stopped successfully.</summary>
	public const int JobWatcherServiceStoppedSuccessfully = 147107;

	/// <summary>Error stopping job watcher service.</summary>
	public const int ErrorStoppingJobWatcherService = 147108;

	/// <summary>Pausing job.</summary>
	public const int PausingJob = 147109;

	/// <summary>Resuming job.</summary>
	public const int ResumingJob = 147110;

	/// <summary>Error creating job config watcher service.</summary>
	public const int ErrorCreatingJobConfigWatcherService = 147111;

	// ========================================
	// 147200-147249: CDC Job
	// ========================================

	/// <summary>CDC job starting.</summary>
	public const int CdcJobStarting = 147200;

	/// <summary>CDC job completed.</summary>
	public const int CdcJobCompleted = 147201;

	/// <summary>CDC job error.</summary>
	public const int CdcJobError = 147202;

	/// <summary>CDC processing error.</summary>
	public const int CdcProcessingError = 147203;

	// ========================================
	// 147250-147299: Data Processing Job
	// ========================================

	/// <summary>Data processing job starting.</summary>
	public const int DataProcessingJobStarting = 147250;

	/// <summary>Data processing job completed.</summary>
	public const int DataProcessingJobCompleted = 147251;

	/// <summary>Data processing job error.</summary>
	public const int DataProcessingJobError = 147252;

	// ========================================
	// 147300-147399: Quartz Job Adapter
	// ========================================

	/// <summary>Job type not found or invalid.</summary>
	public const int JobTypeNotFoundOrInvalid = 147300;

	/// <summary>Could not resolve job type.</summary>
	public const int CouldNotResolveJobType = 147301;

	/// <summary>Executing job.</summary>
	public const int ExecutingJob = 147302;

	/// <summary>Job does not implement interface.</summary>
	public const int JobDoesNotImplementInterface = 147303;

	/// <summary>Job completed successfully.</summary>
	public const int JobCompletedSuccessfully = 147304;

	/// <summary>Error executing job.</summary>
	public const int ErrorExecutingJob = 147305;

	// ========================================
	// 147400-147409: Redis Job Coordinator
	// ========================================

	/// <summary>Acquired distributed lock for job.</summary>
	public const int RedisLockAcquired = 147400;

	/// <summary>Failed to acquire distributed lock for job.</summary>
	public const int RedisLockAcquisitionFailed = 147401;

	/// <summary>Registered job processing instance.</summary>
	public const int RedisInstanceRegistered = 147402;

	/// <summary>Unregistered job processing instance.</summary>
	public const int RedisInstanceUnregistered = 147403;

	/// <summary>Failed to deserialize instance info.</summary>
	public const int RedisInstanceDeserializationFailed = 147404;

	/// <summary>Instance elected as leader.</summary>
	public const int RedisLeaderElected = 147405;

	/// <summary>Failed to deserialize leader info.</summary>
	public const int RedisLeaderDeserializationFailed = 147406;

	/// <summary>Distributed job to instance.</summary>
	public const int RedisJobDistributed = 147407;

	/// <summary>No available instances found to process job.</summary>
	public const int RedisNoInstanceAvailable = 147408;

	/// <summary>Reported completion for job.</summary>
	public const int RedisJobCompletionReported = 147409;

	// ========================================
	// 147500-147509: Health Check Job
	// ========================================

	/// <summary>Starting health check job.</summary>
	public const int HealthCheckJobStarting = 147500;

	/// <summary>Health check completed.</summary>
	public const int HealthCheckJobCompleted = 147501;

	/// <summary>Health check warning.</summary>
	public const int HealthCheckWarning = 147502;

	/// <summary>Health check threw exception.</summary>
	public const int HealthCheckError = 147503;

	/// <summary>Health check data.</summary>
	public const int HealthCheckData = 147504;

	/// <summary>Unexpected error in health check job.</summary>
	public const int HealthCheckJobFailed = 147505;

	// ========================================
	// 147510-147519: Outbox Processor Job
	// ========================================

	/// <summary>Starting outbox processing job.</summary>
	public const int OutboxProcessorJobStarting = 147510;

	/// <summary>No outbox implementation found.</summary>
	public const int OutboxProcessorOutboxMissing = 147511;

	/// <summary>Outbox processing job completed.</summary>
	public const int OutboxProcessorJobCompleted = 147512;

	/// <summary>No pending outbox messages found.</summary>
	public const int OutboxProcessorNoMessages = 147513;

	/// <summary>Unexpected error in outbox processing job.</summary>
	public const int OutboxProcessorJobFailed = 147514;

	// ========================================
	// 147520-147529: Outbox Job
	// ========================================

	/// <summary>Starting execution of outbox job.</summary>
	public const int OutboxJobExecutionStarting = 147520;

	/// <summary>Completed execution of outbox job.</summary>
	public const int OutboxJobExecutionCompleted = 147521;

	/// <summary>Error executing outbox job.</summary>
	public const int OutboxJobExecutionFailed = 147522;

	// ========================================
	// 147600-147609: Quartz Generic Job Adapter
	// ========================================

	/// <summary>Failed to deserialize job context.</summary>
	public const int GenericContextDeserializationFailed = 147600;

	/// <summary>Context not found or invalid in JobDataMap.</summary>
	public const int GenericContextNotFoundOrInvalid = 147601;

	/// <summary>Executing generic job.</summary>
	public const int ExecutingGenericJob = 147602;

	/// <summary>Generic job completed successfully.</summary>
	public const int GenericJobCompletedSuccessfully = 147603;

	/// <summary>Error executing generic job.</summary>
	public const int GenericJobExecutionFailed = 147604;

	// ========================================
	// 147700-147709: Workflow Job
	// ========================================

	/// <summary>Workflow job starting.</summary>
	public const int WorkflowJobStarting = 147700;

	/// <summary>Workflow job completed successfully.</summary>
	public const int WorkflowJobCompleted = 147701;

	/// <summary>Workflow job failed.</summary>
	public const int WorkflowJobFailed = 147702;

	/// <summary>Unhandled exception in workflow job.</summary>
	public const int WorkflowJobUnhandledException = 147703;
}
