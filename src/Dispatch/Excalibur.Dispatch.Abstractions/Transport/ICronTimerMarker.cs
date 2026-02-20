// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Marker interface for typed cron timer messages.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface with an empty struct or class to create a typed cron timer.
/// This enables strongly-typed handler routing where each handler only receives
/// events from its specific timer.
/// </para>
/// <para>
/// <strong>Why use typed timers?</strong>
/// </para>
/// <para>
/// Without typed timers, all handlers of <c>CronTimerTriggerMessage</c> receive
/// events from ALL registered timers, requiring manual filtering:
/// </para>
/// <code>
/// // Without typed timers - BAD: must filter manually
/// public class CleanupHandler : IMessageHandler&lt;CronTimerTriggerMessage&gt;
/// {
///     public Task HandleAsync(CronTimerTriggerMessage message, ...)
///     {
///         if (message.TimerName != "cleanup") return Task.CompletedTask; // Ugly!
///         // actual work...
///     }
/// }
/// </code>
/// <para>
/// With typed timers, handlers automatically receive only their specific timer events:
/// </para>
/// <code>
/// // With typed timers - GOOD: automatic routing
/// public struct CleanupTimer : ICronTimerMarker { }
///
/// public class CleanupHandler : IMessageHandler&lt;CronTimerTriggerMessage&lt;CleanupTimer&gt;&gt;
/// {
///     public Task HandleAsync(CronTimerTriggerMessage&lt;CleanupTimer&gt; message, ...)
///     {
///         // This handler ONLY receives CleanupTimer events - no filtering needed!
///     }
/// }
/// </code>
/// </remarks>
/// <example>
/// <code>
/// // 1. Define timer markers (empty structs recommended for zero allocation)
/// public struct CleanupTimer : ICronTimerMarker { }
/// public struct DailyReportTimer : ICronTimerMarker { }
/// public struct HealthCheckTimer : ICronTimerMarker { }
///
/// // 2. Register timers with their marker types
/// services.AddCronTimerTransport&lt;CleanupTimer&gt;("*/5 * * * *");
/// services.AddCronTimerTransport&lt;DailyReportTimer&gt;("0 0 * * *");
/// services.AddCronTimerTransport&lt;HealthCheckTimer&gt;("*/30 * * * * *"); // Every 30 seconds
///
/// // 3. Create handlers for each timer type
/// public class CleanupHandler : IMessageHandler&lt;CronTimerTriggerMessage&lt;CleanupTimer&gt;&gt;
/// {
///     public Task HandleAsync(CronTimerTriggerMessage&lt;CleanupTimer&gt; message, ...) { }
/// }
/// </code>
/// </example>
/// <seealso cref="CronTimerTriggerMessage{TTimer}"/>
public interface ICronTimerMarker;
