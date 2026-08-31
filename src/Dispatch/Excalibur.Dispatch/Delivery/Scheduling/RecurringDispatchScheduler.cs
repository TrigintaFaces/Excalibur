// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Scheduler with timezone-aware cron support and advanced job management.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RecurringDispatchScheduler" /> class. </remarks>
internal sealed partial class RecurringDispatchScheduler(
	IScheduleStore scheduleStore,
	DispatchJsonSerializer serializer,
	IOptions<SchedulerOptions> options,
	ICronScheduler cronScheduler,
	IOptions<CronScheduleOptions> cronOptions,
	ILogger<RecurringDispatchScheduler> logger,
	TimeProvider? timeProvider = null) : IDispatchScheduler
{
	private readonly SchedulerOptions _options = options.Value;
	private readonly CronScheduleOptions _cronOptions = cronOptions.Value;

	// due/past-schedule and next-occurrence decisions read the clock through TimeProvider so the
	// boundary is testable (default TimeProvider.System keeps existing DI/callers unchanged).
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code.")]
	[RequiresDynamicCode("JSON serialization with a runtime type requires runtime code generation.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046",
			Justification = "The requirement is declared on this implementation, not on IDispatchScheduler. The interface omits it deliberately: DeliveryServiceCollectionExtensions.AddDispatchScheduling registers this type with TryAddSingleton and AddDispatchScheduler<TScheduler> replaces it outright, so a consumer who supplies their own IDispatchScheduler never reaches this reflective path; annotating the interface would warn at every call site in a composition that does not reflect.")]
	[UnconditionalSuppressMessage("AOT", "IL3051",
			Justification = "The requirement is declared on this implementation, not on IDispatchScheduler. The interface omits it deliberately: DeliveryServiceCollectionExtensions.AddDispatchScheduling registers this type with TryAddSingleton and AddDispatchScheduler<TScheduler> replaces it outright, so a consumer who supplies their own IDispatchScheduler never reaches this reflective path; annotating the interface would warn at every call site in a composition that does not reflect.")]
	public async Task ScheduleOnceAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			DateTimeOffset executeAtUtc, TMessage message,
			CancellationToken cancellationToken)
			where TMessage : class
	{
		if (executeAtUtc < _timeProvider.GetUtcNow())
		{
			if (_options.PastScheduleBehavior == PastScheduleBehavior.Reject)
			{
				throw new ArgumentOutOfRangeException(nameof(executeAtUtc));
			}

			executeAtUtc = _timeProvider.GetUtcNow();
		}

		var type = typeof(TMessage);
		// A schedule row outlives the process that wrote it, so its type name must not encode anything that
		// changes between releases. An assembly-qualified name embeds the assembly version, and message types
		// live in the consumer's assembly -- so every persisted row would stop resolving the first time they
		// shipped a new version, and the schedule would silently never fire again. The bare full name carries
		// no version and is the form the inbox and outbox writers store.
		var name = type.FullName ?? type.Name;
		var body = await serializer.SerializeAsync(message, typeof(TMessage)).ConfigureAwait(false);

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
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code.")]
	[RequiresDynamicCode("JSON serialization with a runtime type requires runtime code generation.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046",
			Justification = "The requirement is declared on this implementation, not on IDispatchScheduler. The interface omits it deliberately: DeliveryServiceCollectionExtensions.AddDispatchScheduling registers this type with TryAddSingleton and AddDispatchScheduler<TScheduler> replaces it outright, so a consumer who supplies their own IDispatchScheduler never reaches this reflective path; annotating the interface would warn at every call site in a composition that does not reflect.")]
	[UnconditionalSuppressMessage("AOT", "IL3051",
			Justification = "The requirement is declared on this implementation, not on IDispatchScheduler. The interface omits it deliberately: DeliveryServiceCollectionExtensions.AddDispatchScheduling registers this type with TryAddSingleton and AddDispatchScheduler<TScheduler> replaces it outright, so a consumer who supplies their own IDispatchScheduler never reaches this reflective path; annotating the interface would warn at every call site in a composition that does not reflect.")]
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
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code.")]
	[RequiresDynamicCode("JSON serialization with a runtime type requires runtime code generation.")]
	public async Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			string cronExpression, TimeZoneInfo timeZone, TMessage message,
			CancellationToken cancellationToken)
			where TMessage : class
	{
		var type = typeof(TMessage);
		// A schedule row outlives the process that wrote it, so its type name must not encode anything that
		// changes between releases. An assembly-qualified name embeds the assembly version, and message types
		// live in the consumer's assembly -- so every persisted row would stop resolving the first time they
		// shipped a new version, and the schedule would silently never fire again. The bare full name carries
		// no version and is the form the inbox and outbox writers store.
		var name = type.FullName ?? type.Name;

		// Validate cron expression using our cron scheduler
		var cronExpr = cronScheduler.Parse(cronExpression, timeZone);
		var body = await serializer.SerializeAsync(message, typeof(TMessage)).ConfigureAwait(false);

		// Calculate next execution time
		var nextRun = cronExpr.GetNextOccurrenceUtc(_timeProvider.GetUtcNow());

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
	[RequiresUnreferencedCode("JSON serialization with runtime type may require unreferenced code.")]
	[RequiresDynamicCode("JSON serialization with a runtime type requires runtime code generation.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046",
			Justification = "The requirement is declared on this implementation, not on IDispatchScheduler. The interface omits it deliberately: DeliveryServiceCollectionExtensions.AddDispatchScheduling registers this type with TryAddSingleton and AddDispatchScheduler<TScheduler> replaces it outright, so a consumer who supplies their own IDispatchScheduler never reaches this reflective path; annotating the interface would warn at every call site in a composition that does not reflect.")]
	[UnconditionalSuppressMessage("AOT", "IL3051",
			Justification = "The requirement is declared on this implementation, not on IDispatchScheduler. The interface omits it deliberately: DeliveryServiceCollectionExtensions.AddDispatchScheduling registers this type with TryAddSingleton and AddDispatchScheduler<TScheduler> replaces it outright, so a consumer who supplies their own IDispatchScheduler never reaches this reflective path; annotating the interface would warn at every call site in a composition that does not reflect.")]
	public async Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(
			TimeSpan interval,
			TMessage message,
			CancellationToken cancellationToken)
			where TMessage : class
	{
		var type = typeof(TMessage);
		// A schedule row outlives the process that wrote it, so its type name must not encode anything that
		// changes between releases. An assembly-qualified name embeds the assembly version, and message types
		// live in the consumer's assembly -- so every persisted row would stop resolving the first time they
		// shipped a new version, and the schedule would silently never fire again. The bare full name carries
		// no version and is the form the inbox and outbox writers store.
		var name = type.FullName ?? type.Name;
		var body = await serializer.SerializeAsync(message, typeof(TMessage)).ConfigureAwait(false);

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
			NextExecutionUtc = _timeProvider.GetUtcNow().Add(interval),
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

	/// <summary>
	/// Resolves the tenant to persist on the schedule: the message's own <see cref="ITenantAware.TenantId"/>
	/// when it declares one, otherwise the ambient tenant established at the moment of scheduling (never
	/// overwrites a more specific source; a message-level value always wins).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The message-level check is not merely a formality kept for symmetry with the ambient fallback: it
	/// is the ONLY correct source when a caller schedules a message on behalf of a tenant other than its
	/// own ambient one (e.g. an admin tool scheduling per-tenant reports from an unscoped context).
	/// </para>
	/// <para>
	/// Reads <see cref="TenantContextHolder.Current"/> directly rather than resolving
	/// <see cref="ITenantContext"/> through DI -- the default single-tenant registration's
	/// <c>TenantId</c> is a fixed constant that ignores the ambient holder entirely, so a caller with no
	/// ambient tenant established would otherwise get the default tenant stamped on every schedule
	/// regardless. A deployment with no ambient tenant established stays untenanted here, exactly as it
	/// did before this fallback existed.
	/// </para>
	/// </remarks>
	private static string? ExtractTenantId<TMessage>(TMessage message) =>
		message is ITenantAware aware ? aware.TenantId : TenantContextHolder.Current;

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
