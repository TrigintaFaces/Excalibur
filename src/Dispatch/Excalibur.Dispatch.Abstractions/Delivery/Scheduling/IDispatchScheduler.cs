// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines the contract for scheduling message dispatch operations with support for both one-time and recurring execution patterns.
/// </summary>
/// <remarks>
/// The dispatch scheduler is a fundamental component for time-based message processing in distributed systems, enabling deferred execution,
/// periodic tasks, and complex scheduling scenarios. It provides flexibility for various temporal messaging patterns while maintaining
/// reliability and scalability.
/// <para> <strong> Scheduling Patterns: </strong> </para>
/// The scheduler supports multiple execution patterns:
/// - One-time scheduling for delayed message processing.
/// - Cron-based recurring schedules for complex timing requirements.
/// - Interval-based recurring schedules for simple periodic tasks.
/// - Timezone-aware scheduling for global distributed systems.
/// <para> <strong> Reliability Features: </strong> </para>
/// Implementations should provide:
/// - Persistent schedule storage to survive system restarts.
/// - Missed execution handling for system downtime scenarios.
/// - Duplicate execution prevention through idempotency mechanisms.
/// - Monitoring and observability for scheduled job tracking.
/// <para> <strong> Scalability Considerations: </strong> </para>
/// The scheduler should be designed for:
/// - High-volume scheduling with efficient storage and retrieval.
/// - Distributed execution across multiple nodes.
/// - Load balancing for scheduled job processing.
/// - Graceful degradation under high load conditions.
/// </remarks>
public interface IDispatchScheduler
{
	/// <summary>
	/// Schedules a message for one-time dispatch at the specified future time.
	/// </summary>
	/// <typeparam name="TMessage"> The message type to schedule. Must be a reference type to support serialization. </typeparam>
	/// <param name="executeAtUtc"> The UTC timestamp when the message should be dispatched. Must be in the future. </param>
	/// <param name="message"> The message instance to dispatch at the scheduled time. Cannot be null. </param>
	/// <param name="cancellationToken"> Token to observe for cancellation requests during the scheduling operation. </param>
	/// <returns> A task representing the asynchronous scheduling operation. </returns>
	/// <remarks>
	/// One-time scheduling is ideal for scenarios such as:
	/// <para> <strong> Use Cases: </strong> </para>
	/// - Delayed notification delivery (e.g., reminder emails after registration).
	/// - Timeout handling for long-running processes.
	/// - Scheduled data processing tasks (e.g., report generation).
	/// - Deferred cleanup operations after resource usage.
	/// <para> <strong> Execution Guarantees: </strong> </para>
	/// The message will be dispatched approximately at the specified time, subject to:
	/// - System clock accuracy and scheduler polling intervals
	/// - Load conditions and processing capacity at execution time
	/// - Network latency and message broker availability
	/// <para> <strong> Error Handling: </strong> </para>
	/// If scheduling fails, the operation will throw an exception. If execution fails at the scheduled time, the behavior depends on the
	/// configured retry policies and dead letter handling strategies.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when message is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when executeAtUtc is in the past. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	Task ScheduleOnceAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(DateTimeOffset executeAtUtc,
		TMessage message, CancellationToken cancellationToken)
		where TMessage : class;

	/// <summary>
	/// Schedules a message for recurring dispatch based on a cron expression schedule.
	/// </summary>
	/// <typeparam name="TMessage"> The message type to schedule. Must be a reference type to support serialization. </typeparam>
	/// <param name="cronExpression"> The cron expression defining the execution schedule. Must be a valid cron format. </param>
	/// <param name="message"> The message instance template to dispatch on each scheduled execution. Cannot be null. </param>
	/// <param name="cancellationToken"> Token to observe for cancellation requests during the scheduling operation. </param>
	/// <returns> A task representing the asynchronous scheduling operation. </returns>
	/// <remarks>
	/// Cron-based scheduling provides powerful flexibility for complex timing requirements:
	/// <para> <strong> Cron Expression Support: </strong> </para>
	/// Standard cron format: "* * * * * *" (seconds, minutes, hours, day of month, month, day of week).
	/// - "0 0 12 * * ?" - Daily at noon.
	/// - "0 0/15 * * * ?" - Every 15 minutes.
	/// - "0 0 9 * * MON-FRI" - Weekdays at 9 AM.
	/// - "0 0 0 1 * ?" - First day of each month.
	/// <para> <strong> Advanced Features: </strong> </para>
	/// Implementations may support:
	/// - Timezone-aware execution for global systems
	/// - Missed execution policies for system downtime handling
	/// - Maximum execution count limits for finite recurring schedules
	/// - Dynamic schedule modification during runtime
	/// <para> <strong> Message Templating: </strong> </para>
	/// The provided message serves as a template. Each execution may create a new message instance with updated timestamps or
	/// execution-specific data.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when cronExpression or message is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when cronExpression is invalid or malformed. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(string cronExpression,
		TMessage message, CancellationToken cancellationToken)
		where TMessage : class;

	/// <summary>
	/// Schedules a message for recurring dispatch at regular time intervals.
	/// </summary>
	/// <typeparam name="TMessage"> The message type to schedule. Must be a reference type to support serialization. </typeparam>
	/// <param name="interval"> The time interval between consecutive message dispatches. Must be positive. </param>
	/// <param name="message"> The message instance template to dispatch on each scheduled execution. Cannot be null. </param>
	/// <param name="cancellationToken"> Token to observe for cancellation requests during the scheduling operation. </param>
	/// <returns> A task representing the asynchronous scheduling operation. </returns>
	/// <remarks>
	/// Interval-based scheduling provides simple, predictable recurring execution:
	/// <para> <strong> Interval Patterns: </strong> </para>
	/// Common interval scheduling patterns include:
	/// - Health checks every 30 seconds: TimeSpan.FromSeconds(30).
	/// - Data synchronization every 5 minutes: TimeSpan.FromMinutes(5).
	/// - Daily report generation: TimeSpan.FromDays(1).
	/// - Hourly cleanup tasks: TimeSpan.FromHours(1).
	/// <para> <strong> Execution Timing: </strong> </para>
	/// The first execution occurs after the specified interval from the scheduling time. Subsequent executions are scheduled based on the
	/// completion time of the previous execution, preventing overlap and accumulation of delayed executions.
	/// <para> <strong> Performance Considerations: </strong> </para>
	/// Very short intervals (seconds or less) may impact system performance. Consider the processing time of the message and system
	/// capacity when selecting intervals for high-frequency recurring schedules.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when message is null. </exception>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when interval is zero or negative. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	Task ScheduleRecurringAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMessage>(TimeSpan interval,
		TMessage message, CancellationToken cancellationToken)
		where TMessage : class;
}
