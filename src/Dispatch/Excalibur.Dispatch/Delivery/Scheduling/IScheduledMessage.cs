// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Represents a persistable scheduled message with comprehensive metadata for timezone-aware execution and tracking.
/// </summary>
/// <remarks>
/// This interface defines the contract for scheduled messages that support both one-time and recurring execution patterns with rich
/// metadata for enterprise scenarios including multi-tenancy, distributed tracing, and execution history tracking.
/// <para> <strong> Execution Models: </strong> </para>
/// The interface supports multiple execution patterns:
/// - Cron-based recurring schedules with timezone awareness
/// - Simple interval-based recurring schedules
/// - One-time execution schedules (NextExecutionUtc without recurrence)
/// - Conditional execution based on enabled state and business rules.
/// <para> <strong> Enterprise Features: </strong> </para>
/// Built-in support for enterprise requirements:
/// - Multi-tenancy through TenantId for isolated scheduling
/// - Distributed tracing integration through TraceParent
/// - Audit trails with creation and execution tracking
/// - Correlation tracking for related message processing.
/// <para> <strong> Persistence Considerations: </strong> </para>
/// This interface is designed for persistence implementations that require:
/// - Serialized message storage with type information
/// - Efficient querying by next execution time
/// - Support for enabling/disabling schedules dynamically
/// - Historical execution tracking for monitoring and debugging.
/// </remarks>
public interface IScheduledMessage
{
	/// <summary>
	/// Gets or sets the correlation identifier for linking related messages and operations.
	/// </summary>
	/// <value>
	/// A correlation ID that can be used to group related messages across distributed operations. Null indicates no specific correlation is
	/// required for this scheduled message.
	/// </value>
	/// <remarks>
	/// Correlation IDs enable:
	/// - Linking scheduled messages to the operations that created them
	/// - Grouping related messages for saga or workflow processing
	/// - Tracking message flows across distributed system boundaries
	/// - Debugging and troubleshooting complex message processing scenarios.
	/// </remarks>
	string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the cron expression that defines the recurring execution schedule.
	/// </summary>
	/// <value>
	/// A valid cron expression string defining when the message should be executed. Used only for recurring schedules; ignored for one-time executions.
	/// </value>
	/// <remarks>
	/// The cron expression follows standard cron syntax with seconds precision: "seconds minutes hours day-of-month month day-of-week".
	/// <para> <strong> Common Patterns: </strong> </para>
	/// <para>
	/// - "0 0 12 * * ?" - Daily at noon
	/// - "0 */15 * * * ?" - Every 15 minutes
	/// - "0 0 9 * * MON-FRI" - Weekdays at 9 AM
	/// - "0 0 0 1 * ?" - First day of each month
	/// </para>
	/// <para>The expression is evaluated in the context of the TimeZoneId property to support timezone-aware scheduling for global applications.</para>
	/// </remarks>
	string CronExpression { get; set; }

	/// <summary>
	/// Gets or sets the timezone identifier for evaluating cron expressions in local time.
	/// </summary>
	/// <value>
	/// A standard timezone identifier (e.g., "America/New_York", "Europe/London"). Null or empty indicates UTC timezone should be used for evaluation.
	/// </value>
	/// <remarks>
	/// Timezone awareness is crucial for global applications where scheduled operations need to respect local business hours, daylight
	/// saving time transitions, and regional compliance requirements.
	/// <para> <strong> Timezone Handling: </strong> </para>
	/// - Uses standard IANA timezone identifiers for consistency
	/// - Automatically handles daylight saving time transitions
	/// - Ensures consistent execution times across timezone changes
	/// - Supports timezone-specific business logic and compliance.
	/// </remarks>
	string? TimeZoneId { get; set; }

	/// <summary>
	/// Gets or sets the fixed interval for simple recurring schedule patterns.
	/// </summary>
	/// <value>
	/// A TimeSpan representing the interval between executions for simple recurring schedules. Null indicates this is not an interval-based schedule.
	/// </value>
	/// <remarks>
	/// Interval-based scheduling provides a simpler alternative to cron expressions for straightforward recurring patterns:
	/// <para> <strong> Usage Patterns: </strong> </para>
	/// - Health checks: TimeSpan.FromSeconds(30)
	/// - Data synchronization: TimeSpan.FromMinutes(5)
	/// - Batch processing: TimeSpan.FromHours(1)
	/// - Cleanup operations: TimeSpan.FromDays(1)
	///
	/// When both CronExpression and Interval are specified, the implementation should prioritize the CronExpression for more precise timing control.
	/// </remarks>
	TimeSpan? Interval { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this scheduled message is active and should be executed.
	/// </summary>
	/// <value> True if the schedule is active and should be executed; false to pause execution without deleting the schedule definition. </value>
	/// <remarks>
	/// The enabled flag provides runtime control over schedule execution:
	/// <para> <strong> Use Cases: </strong> </para>
	/// <para>
	/// - Temporarily pausing schedules during maintenance windows
	/// - Feature flag integration for gradual schedule rollouts
	/// - A/B testing with different scheduling strategies
	/// - Emergency suspension of problematic scheduled operations
	/// </para>
	/// <para>Disabled schedules should be preserved in storage but skipped during execution planning and processing cycles.</para>
	/// </remarks>
	bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the unique identifier for this scheduled message instance.
	/// </summary>
	/// <value> A globally unique identifier that distinguishes this scheduled message from all other scheduled messages in the system. </value>
	/// <remarks>
	/// The ID serves as the primary key for persistence operations and enables:
	/// - Idempotent schedule creation and updates
	/// - Efficient retrieval and modification of specific schedules
	/// - Foreign key relationships with execution history and audit logs
	/// - Distributed coordination across multiple scheduler instances.
	/// </remarks>
	Guid Id { get; set; }

	/// <summary>
	/// Gets or sets the serialized message payload that will be dispatched when the schedule executes.
	/// </summary>
	/// <value>
	/// A string containing the serialized message data in the format expected by the configured message serializer (JSON, MessagePack, etc.).
	/// </value>
	/// <remarks>
	/// <para>
	/// The message body is stored in serialized form to enable:
	/// - Persistence across system restarts and deployments
	/// - Type-safe deserialization at execution time
	/// - Support for complex message types and nested objects
	/// - Version compatibility through message evolution patterns
	/// </para>
	/// <para>The serialization format should be consistent with the MessageName type information to ensure successful deserialization and execution.</para>
	/// </remarks>
	string MessageBody { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified type name of the message for deserialization.
	/// </summary>
	/// <value>
	/// The complete type name including namespace that can be used to deserialize the MessageBody back into the appropriate message type instance.
	/// </value>
	/// <remarks>
	/// The message name enables type-safe deserialization and includes:
	/// <para> <strong> Type Information: </strong> </para>
	/// <para>
	/// - Full namespace and class name for precise type resolution
	/// - Assembly information if required for cross-assembly scenarios
	/// - Version information for message evolution compatibility
	/// </para>
	/// <para>This information is essential for reconstructing the original message type from persisted storage during schedule execution.</para>
	/// </remarks>
	string MessageName { get; set; }

	/// <summary>
	/// Gets or sets the next scheduled execution time in UTC for efficient querying and execution planning.
	/// </summary>
	/// <value> The UTC timestamp when this message should next be executed, or null if the schedule is complete or indefinitely suspended. </value>
	/// <remarks>
	/// The next execution time is calculated based on:
	/// - Cron expression evaluation in the specified timezone
	/// - Interval-based calculations from the last execution time
	/// - Missed execution policies and recovery strategies
	/// - Schedule enable/disable state and business rules.
	/// <para> <strong> Query Optimization: </strong> </para>
	/// Storing the next execution time in UTC enables efficient database queries for finding all schedules ready for execution without
	/// needing to evaluate cron expressions or intervals at query time.
	/// </remarks>
	DateTimeOffset? NextExecutionUtc { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scheduling isolation.
	/// </summary>
	/// <value> The tenant ID that owns this scheduled message, or null for system-wide schedules. </value>
	/// <remarks>
	/// Multi-tenancy support enables:
	/// - Isolated scheduling per customer or organization
	/// - Tenant-specific execution policies and resource limits
	/// - Separate billing and monitoring per tenant
	/// - Security isolation between different tenant schedules
	///
	/// The tenant ID should be used for filtering and access control throughout the scheduling system to ensure proper isolation.
	/// </remarks>
	string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the distributed tracing parent identifier for observability integration.
	/// </summary>
	/// <value>
	/// The trace parent ID following W3C Trace Context specification for linking scheduled message execution with the original request trace.
	/// </value>
	/// <remarks>
	/// Distributed tracing integration enables:
	/// - End-to-end visibility from request to scheduled execution
	/// - Performance monitoring across asynchronous boundaries
	/// - Error tracking and root cause analysis
	/// - Service dependency mapping and latency analysis
	///
	/// The trace parent should be propagated from the original request context and used to create child spans during schedule execution.
	/// </remarks>
	string? TraceParent { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the user or service that created this schedule.
	/// </summary>
	/// <value>
	/// The user ID, service account, or system identifier responsible for creating this scheduled message, or null for system-generated schedules.
	/// </value>
	/// <remarks>
	/// User tracking enables:
	/// - Audit trails for schedule creation and modifications
	/// - Access control and authorization for schedule management
	/// - User-specific quotas and rate limiting
	/// - Debugging and support for user-reported scheduling issues.
	/// </remarks>
	string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the most recent execution attempt in UTC.
	/// </summary>
	/// <value> The UTC timestamp when this schedule was last executed, or null if it has never been executed. </value>
	/// <remarks>
	/// Execution tracking enables:
	/// - Calculation of next execution times for interval-based schedules
	/// - Monitoring and alerting for overdue or stuck schedules
	/// - Performance analysis and execution frequency tracking
	/// - Debugging and troubleshooting execution issues
	///
	/// This timestamp should be updated regardless of execution success or failure to provide accurate execution history.
	/// </remarks>
	DateTimeOffset? LastExecutionUtc { get; set; }

	/// <summary>
	/// Gets or sets the policy for handling executions that were missed due to system downtime or delays.
	/// </summary>
	/// <value> The missed execution behavior policy, or null to use system defaults. </value>
	/// <remarks>
	/// Missed execution handling is critical for reliable scheduling:
	/// <para> <strong> Policy Options: </strong> </para>
	/// - Execute immediately upon system recovery
	/// - Skip missed executions and wait for next scheduled time
	/// - Execute once with consolidated processing for multiple missed executions
	/// - Apply business-specific rules based on message type and context
	///
	/// The policy should be chosen based on the criticality and idempotency characteristics of the scheduled operation.
	/// </remarks>
	MissedExecutionBehavior? MissedExecutionBehavior { get; set; }
}
