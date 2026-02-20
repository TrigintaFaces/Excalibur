// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Jobs;

internal static partial class OutboxProcessorJobLog
{
	[LoggerMessage(JobsEventId.OutboxProcessorJobStarting, LogLevel.Debug, "Starting outbox processing job.")]
	public static partial void JobStarting(ILogger logger);

	[LoggerMessage(JobsEventId.OutboxProcessorOutboxMissing, LogLevel.Warning, "No outbox implementation found, skipping processing.")]
	public static partial void OutboxMissing(ILogger logger);

	[LoggerMessage(JobsEventId.OutboxProcessorJobCompleted, LogLevel.Information, "Outbox processing job completed, processed {MessageCount} messages.")]
	public static partial void JobCompleted(ILogger logger, int messageCount);

	[LoggerMessage(JobsEventId.OutboxProcessorNoMessages, LogLevel.Debug, "No pending outbox messages found.")]
	public static partial void NoMessages(ILogger logger);

	[LoggerMessage(JobsEventId.OutboxProcessorJobFailed, LogLevel.Error, "Unexpected error in outbox processing job.")]
	public static partial void JobFailed(ILogger logger, Exception exception);
}
