// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering CronTimer transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for CronTimer transport configuration.
/// </para>
/// <para>
/// The CronTimer transport is a trigger-only transport that generates synthetic
/// <see cref="CronTimerTriggerMessage"/> instances at scheduled intervals based
/// on a cron expression.
/// </para>
/// <para>
/// <strong>Prerequisites:</strong> Register <see cref="ICronScheduler"/> before adding cron timers:
/// <code>
/// services.AddSingleton&lt;ICronScheduler, CronScheduler&gt;();
/// services.AddCronTimerTransport("cleanup", "*/5 * * * *");
/// </code>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Register a cron timer that fires every 5 minutes
/// services.AddCronTimerTransport("cleanup", "*/5 * * * *");
///
/// // Register multiple cron timers with options
/// services.AddCronTimerTransport("hourly-report", "0 * * * *");
/// services.AddCronTimerTransport("daily-cleanup", "0 0 * * *", opts =>
/// {
///     opts.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
///     opts.RunOnStartup = true;
/// });
///
/// // Type-safe timers (recommended for multiple timers)
/// services.AddCronTimerTransport&lt;CleanupTimer&gt;("*/5 * * * *");
/// services.AddCronTimerTransport&lt;DailyReportTimer&gt;("0 0 * * *");
/// </code>
/// </example>
public static class CronTimerTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "crontimer";

	/// <summary>
	/// Adds a CronTimer transport with the specified name and cron expression.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name for multi-transport routing.</param>
	/// <param name="cronExpression">The cron expression (e.g., "*/5 * * * *" for every 5 minutes).</param>
	/// <param name="configure">Optional transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> or <paramref name="cronExpression"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary entry point for CronTimer transport configuration.
	/// </para>
	/// <para>
	/// <strong>Important:</strong> Ensure <see cref="ICronScheduler"/> is registered in DI before
	/// adding cron timers, otherwise a runtime error will occur when the transport starts.
	/// </para>
	/// <para>
	/// Cron expression examples:
	/// <list type="bullet">
	///   <item><description>"0 * * * *" - Every hour at minute 0</description></item>
	///   <item><description>"*/5 * * * *" - Every 5 minutes</description></item>
	///   <item><description>"0 0 * * *" - Daily at midnight</description></item>
	///   <item><description>"0 9 * * 1-5" - Weekdays at 9 AM</description></item>
	///   <item><description>"*/30 * * * * *" - Every 30 seconds (6-field)</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCronTimerTransport("cleanup", "*/5 * * * *", options =>
	/// {
	///     options.RunOnStartup = true;
	///     options.PreventOverlap = true;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddCronTimerTransport(
		this IServiceCollection services,
		string name,
		string cronExpression,
		Action<CronTimerTransportAdapterOptions>? configure = null)
	{
		return AddCronTimerTransportCore(services, name, cronExpression, messageFactory: null, configure);
	}

	/// <summary>
	/// Adds a CronTimer transport with the default name.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="cronExpression">The cron expression.</param>
	/// <param name="configure">Optional transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="cronExpression"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "crontimer".
	/// Use the named overload when you need multiple cron timers.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single cron timer scenario with default name
	/// services.AddCronTimerTransport("0 * * * *");
	/// </code>
	/// </example>
	public static IServiceCollection AddCronTimerTransport(
		this IServiceCollection services,
		string cronExpression,
		Action<CronTimerTransportAdapterOptions>? configure = null)
	{
		return AddCronTimerTransportCore(services, DefaultTransportName, cronExpression, messageFactory: null, configure);
	}

	/// <summary>
	/// Adds a typed CronTimer transport that dispatches <see cref="CronTimerTriggerMessage{TTimer}"/> messages.
	/// </summary>
	/// <typeparam name="TTimer">The timer marker type for typed routing.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="cronExpression">The cron expression (e.g., "*/5 * * * *" for every 5 minutes).</param>
	/// <param name="configure">Optional transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="cronExpression"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This generic overload enables type-safe handler routing. Handlers of
	/// <c>CronTimerTriggerMessage&lt;TTimer&gt;</c> will only receive events from this
	/// specific timer, eliminating the need for manual timer name filtering.
	/// </para>
	/// <para>
	/// The timer name is automatically derived from the marker type name.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Define timer markers (empty structs recommended)
	/// public struct CleanupTimer : ICronTimerMarker { }
	/// public struct DailyReportTimer : ICronTimerMarker { }
	///
	/// // Register typed timers
	/// services.AddCronTimerTransport&lt;CleanupTimer&gt;("*/5 * * * *");
	/// services.AddCronTimerTransport&lt;DailyReportTimer&gt;("0 0 * * *");
	///
	/// // Handlers automatically receive only their specific timer events
	/// public class CleanupHandler : IMessageHandler&lt;CronTimerTriggerMessage&lt;CleanupTimer&gt;&gt;
	/// {
	///     public Task HandleAsync(CronTimerTriggerMessage&lt;CleanupTimer&gt; message, ...) { }
	/// }
	/// </code>
	/// </example>
	/// <seealso cref="ICronTimerMarker"/>
	/// <seealso cref="CronTimerTriggerMessage{TTimer}"/>
	public static IServiceCollection AddCronTimerTransport<TTimer>(
		this IServiceCollection services,
		string cronExpression,
		Action<CronTimerTransportAdapterOptions>? configure = null)
		where TTimer : ICronTimerMarker
	{
		// Use the marker type name as the transport name
		return AddCronTimerTransportCore(
			services,
			typeof(TTimer).Name,
			cronExpression,
			CreateTypedMessageFactory<TTimer>(),
			configure);
	}

	/// <summary>
	/// Adds a typed CronTimer transport with a custom transport name.
	/// </summary>
	/// <typeparam name="TTimer">The timer marker type for typed routing.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="name">Custom transport name (overrides the default type-based name).</param>
	/// <param name="cronExpression">The cron expression.</param>
	/// <param name="configure">Optional transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> or <paramref name="cronExpression"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this overload when you need to specify a custom transport name instead of
	/// using the marker type name.
	/// </para>
	/// </remarks>
	/// <seealso cref="ICronTimerMarker"/>
	public static IServiceCollection AddCronTimerTransport<TTimer>(
		this IServiceCollection services,
		string name,
		string cronExpression,
		Action<CronTimerTransportAdapterOptions>? configure = null)
		where TTimer : ICronTimerMarker
	{
		return AddCronTimerTransportCore(
			services,
			name,
			cronExpression,
			CreateTypedMessageFactory<TTimer>(),
			configure);
	}

	/// <summary>
	/// Core implementation for registering a CronTimer transport.
	/// </summary>
	private static IServiceCollection AddCronTimerTransportCore(
		IServiceCollection services,
		string name,
		string cronExpression,
		Func<string, string, DateTimeOffset, string, CronTimerTriggerMessage>? messageFactory,
		Action<CronTimerTransportAdapterOptions>? configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

		// Create and configure options
		var options = new CronTimerTransportAdapterOptions
		{
			Name = name, CronExpression = cronExpression, MessageFactory = messageFactory,
		};
		configure?.Invoke(options);

		// Register factory in TransportRegistry for lifecycle management
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			CronTimerTransportAdapter.TransportTypeName,
			sp =>
			{
				var cronScheduler = sp.GetService<ICronScheduler>()
				                    ?? throw new InvalidOperationException(
					                    $"ICronScheduler is not registered. Register it before adding cron timers: " +
					                    $"services.AddSingleton<ICronScheduler, CronScheduler>();");

				var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<CronTimerTransportAdapter>()
				             ?? Logging.Abstractions.NullLogger<CronTimerTransportAdapter>.Instance;

				return new CronTimerTransportAdapter(logger, cronScheduler, sp, options);
			});

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();

		return services;
	}

	/// <summary>
	/// Creates a message factory that produces typed <see cref="CronTimerTriggerMessage{TTimer}"/> instances.
	/// </summary>
	private static Func<string, string, DateTimeOffset, string, CronTimerTriggerMessage> CreateTypedMessageFactory<TTimer>()
		where TTimer : ICronTimerMarker
	{
		return (timerName, cronExpression, triggerTime, timeZone) => new CronTimerTriggerMessage<TTimer>
		{
			TimerName = timerName, CronExpression = cronExpression, TriggerTimeUtc = triggerTime, TimeZone = timeZone,
		};
	}
}
