// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Scheduler with timezone-aware cron support and advanced job management.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RecurringDispatchScheduler" /> class. </remarks>
public sealed partial class RecurringDispatchScheduler(
	IScheduleStore scheduleStore,
	IJsonSerializer serializer,
	IOptions<SchedulerOptions> options,
	ICronScheduler cronScheduler,
	IOptions<CronScheduleOptions> cronOptions,
	ILogger<RecurringDispatchScheduler> logger) : IDispatchScheduler
{
	private readonly SchedulerOptions _options = options.Value;
	private readonly CronScheduleOptions _cronOptions = cronOptions.Value;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Scheduling uses runtime serialization; AOT users should opt out of this scheduler or use compatible serializers.")]
	public async Task ScheduleOnceAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			DateTimeOffset executeAtUtc, TMessage message,
			CancellationToken cancellationToken)
			where TMessage : class
	{
		if (executeAtUtc < DateTimeOffset.UtcNow)
		{
			if (_options.PastScheduleBehavior == PastScheduleBehavior.Reject)
			{
				throw new ArgumentOutOfRangeException(nameof(executeAtUtc));
			}

			executeAtUtc = DateTimeOffset.UtcNow;
		}

		var type = typeof(TMessage);
		var name = type.AssemblyQualifiedName ?? type.FullName!;
		var body = await serializer.SerializeAsync(message).ConfigureAwait(false);

		var scheduled = new ScheduledMessage
		{
			MessageName = name,
			MessageBody = body,
			CorrelationId = ExtractCorrelationId(message),
			TraceParent = ExtractTraceParent(),
			TenantId = ExtractTenantId(message),
			UserId = ExtractUserId(),
			Enabled = true,
			Id = Guid.NewGuid(),
			NextExecutionUtc = executeAtUtc,
			TimeZoneId = TimeZoneInfo.Utc.Id,
		};

		await scheduleStore.StoreAsync(scheduled, cancellationToken).ConfigureAwait(false);

		LogScheduledOneTimeMessage(executeAtUtc, type.Name);
	}

	/// <inheritdoc />
	public async Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
		string cronExpression, TMessage message,
		CancellationToken cancellationToken)
		where TMessage : class =>

		// Use default timezone from options
		await ScheduleRecurringAsync(cronExpression, _cronOptions.DefaultTimeZone, message, cancellationToken)
			.ConfigureAwait(false);

	/// <summary>
	/// Schedules a recurring message with a specific timezone.
	/// </summary>
	/// <typeparam name="TMessage"> The message type. </typeparam>
	/// <param name="cronExpression"> The cron expression. </param>
	/// <param name="timeZone"> The timezone for evaluating the cron expression. </param>
	/// <param name="message"> The message to schedule. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Scheduling uses runtime serialization; AOT users should opt out of this scheduler or use compatible serializers.")]
	public async Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			string cronExpression, TimeZoneInfo timeZone, TMessage message,
			CancellationToken cancellationToken)
			where TMessage : class
	{
		var type = typeof(TMessage);
		var name = type.AssemblyQualifiedName ?? type.FullName!;

		// Validate cron expression using our cron scheduler
		var cronExpr = cronScheduler.Parse(cronExpression, timeZone);
		var body = await serializer.SerializeAsync(message).ConfigureAwait(false);

		// Calculate next execution time
		var nextRun = cronExpr.GetNextOccurrenceUtc(DateTimeOffset.UtcNow);

		var entry = new ScheduledMessage
		{
			CronExpression = cronExpression,
			TimeZoneId = timeZone.Id,
			MessageName = name,
			MessageBody = body,
			CorrelationId = ExtractCorrelationId(message),
			TraceParent = ExtractTraceParent(),
			TenantId = ExtractTenantId(message),
			UserId = ExtractUserId(),
			Enabled = true,
			Id = Guid.NewGuid(),
			NextExecutionUtc = nextRun,
			MissedExecutionBehavior = _cronOptions.MissedExecutionBehavior,
		};

		await scheduleStore.StoreAsync(entry, cancellationToken).ConfigureAwait(false);

		LogScheduledRecurringMessageWithCron(type.Name, cronExpression, timeZone.Id);
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Scheduling uses runtime serialization; AOT users should opt out of this scheduler or use compatible serializers.")]
	public async Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			TimeSpan interval,
			TMessage message,
			CancellationToken cancellationToken)
			where TMessage : class
	{
		var type = typeof(TMessage);
		var name = type.AssemblyQualifiedName ?? type.FullName!;
		var body = await serializer.SerializeAsync(message).ConfigureAwait(false);

		var entry = new ScheduledMessage
		{
			Interval = interval,
			MessageName = name,
			MessageBody = body,
			CorrelationId = ExtractCorrelationId(message),
			TraceParent = ExtractTraceParent(),
			TenantId = ExtractTenantId(message),
			UserId = ExtractUserId(),
			Enabled = true,
			Id = Guid.NewGuid(),
			NextExecutionUtc = DateTimeOffset.UtcNow.Add(interval),
			TimeZoneId = TimeZoneInfo.Utc.Id,
		};

		await scheduleStore.StoreAsync(entry, cancellationToken).ConfigureAwait(false);

		LogScheduledRecurringMessageWithInterval(type.Name, interval);
	}

	/// <summary>
	/// Cancels a scheduled message.
	/// </summary>
	/// <param name="scheduleId"> The ID of the schedule to cancel. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the schedule was cancelled; otherwise, false. </returns>
	public async Task<bool> CancelScheduleAsync(Guid scheduleId, CancellationToken cancellationToken)
	{
		await scheduleStore.CompleteAsync(scheduleId, cancellationToken).ConfigureAwait(false);
		return true;
	}

	private static string? ExtractCorrelationId<TMessage>(TMessage message) =>
		message is ICorrelationAware aware ? aware.CorrelationId?.ToString() : null;

	private static string? ExtractTenantId<TMessage>(TMessage message) =>
		message is ITenantAware aware ? aware.TenantId : null;

	private static string? ExtractUserId() =>
		Activity.Current?.GetBaggageItem("user.id");

	private static string? ExtractTraceParent() =>
		Activity.Current?.Id;

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.MessageScheduled, LogLevel.Information,
		"Scheduled one-time message for {Time}: {Type}")]
	private partial void LogScheduledOneTimeMessage(DateTimeOffset time, string type);

	[LoggerMessage(DeliveryEventId.RecurringDispatchScheduled, LogLevel.Information,
		"Scheduled recurring message: {Type} (CRON: {Cron}, Timezone: {TimeZone})")]
	private partial void LogScheduledRecurringMessageWithCron(string type, string cron, string timeZone);

	[LoggerMessage(DeliveryEventId.ScheduledRecurringWithInterval, LogLevel.Information,
		"Scheduled recurring message: {Type} every {Interval}")]
	private partial void LogScheduledRecurringMessageWithInterval(string type, TimeSpan interval);
}
