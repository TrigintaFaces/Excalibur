// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Generic cron timer trigger message that includes timer type information for type-safe routing.
/// </summary>
/// <typeparam name="TTimer">The timer marker type.</typeparam>
/// <remarks>
/// <para>
/// This generic message type enables type-safe handler routing. When you register
/// a cron timer with <c>AddCronTimerTransport&lt;TTimer&gt;</c>, the dispatcher
/// automatically routes messages only to handlers of <c>CronTimerTriggerMessage&lt;TTimer&gt;</c>.
/// </para>
/// <para>
/// This eliminates the need for manual timer name filtering in handlers.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Define a timer marker
/// public struct HourlyCleanupTimer : ICronTimerMarker { }
///
/// // Register the timer
/// services.AddCronTimerTransport&lt;HourlyCleanupTimer&gt;("0 * * * *");
///
/// // Handler receives ONLY HourlyCleanupTimer events
/// public class HourlyCleanupHandler : IMessageHandler&lt;CronTimerTriggerMessage&lt;HourlyCleanupTimer&gt;&gt;
/// {
///     public Task HandleAsync(
///         CronTimerTriggerMessage&lt;HourlyCleanupTimer&gt; message,
///         IDispatchContext context,
///         CancellationToken cancellationToken)
///     {
///         // No need to check TimerName - this handler only gets HourlyCleanupTimer events
///         Console.WriteLine($"Timer type: {message.TimerType.Name}"); // "HourlyCleanupTimer"
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICronTimerMarker"/>
/// <seealso cref="CronTimerTriggerMessage"/>
public record CronTimerTriggerMessage<TTimer> : CronTimerTriggerMessage
	where TTimer : ICronTimerMarker
{
	/// <summary>
	/// Gets the marker type for this timer.
	/// </summary>
	/// <value>The <see cref="Type"/> of the timer marker.</value>
	/// <remarks>
	/// This property provides runtime access to the timer marker type,
	/// useful for logging, diagnostics, or generic processing scenarios.
	/// </remarks>
	public Type TimerType => typeof(TTimer);
}
