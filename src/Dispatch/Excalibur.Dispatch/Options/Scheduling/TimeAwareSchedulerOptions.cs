// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Options.Scheduling;

/// <summary>
/// Enhanced scheduler options with integrated TimePolicy configuration for time-aware scheduled message processing. R7.4: Configurable
/// timeout handling with adaptive capabilities for scheduled message operations.
/// </summary>
public sealed class TimeAwareSchedulerOptions
{
	/// <summary>
	/// The configuration section name for time-aware scheduler options.
	/// </summary>
	public const string SectionName = "Dispatch:TimeAwareScheduler";

	/// <summary>
	/// Cached composite formats for string formatting performance.
	/// </summary>
	private static readonly CompositeFormat TimeoutExceedsMaxFormat =
		CompositeFormat.Parse(ErrorConstants.MessageTypeTimeoutCannotExceedMaxSchedulingTimeout);

	private static readonly CompositeFormat TimeoutMustBePositiveFormat =
		CompositeFormat.Parse(ErrorConstants.MessageTypeTimeoutMustBePositive);

	/// <summary>
	/// Gets or sets how often the scheduler checks for messages to dispatch.
	/// </summary>
	/// <value> How often the scheduler checks for messages to dispatch. </value>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets determines how messages scheduled in the past are handled.
	/// </summary>
	/// <value> The current <see cref="PastScheduleBehavior" /> value. </value>
	public PastScheduleBehavior PastScheduleBehavior { get; set; } = PastScheduleBehavior.ExecuteImmediately;

	/// <summary>
	/// Gets custom timeout overrides for specific message types during scheduling. Key format: "Namespace.MessageTypeName".
	/// </summary>
	/// <value> The current <see cref="MessageTypeSchedulingTimeouts" /> value. </value>
	public Dictionary<string, TimeSpan> MessageTypeSchedulingTimeouts { get; } = [];

	/// <summary>
	/// Gets or sets the complexity multiplier for heavy scheduling operations.
	/// Default: 2.0.
	/// </summary>
	/// <value> The complexity multiplier for heavy scheduling operations. </value>
	[Range(1.0, 5.0)]
	public double HeavyOperationMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets the complexity multiplier for complex scheduling operations.
	/// Default: 1.5.
	/// </summary>
	/// <value> The complexity multiplier for complex scheduling operations. </value>
	[Range(1.0, 3.0)]
	public double ComplexOperationMultiplier { get; set; } = 1.5;

	/// <summary>
	/// Gets or sets timeout configuration for scheduling operations.
	/// </summary>
	/// <value> The timeout sub-options. </value>
	public SchedulerTimeoutOptions Timeouts { get; set; } = new();

	/// <summary>
	/// Gets or sets adaptive and escalation configuration for timeout behavior.
	/// </summary>
	/// <value> The adaptive sub-options. </value>
	public SchedulerAdaptiveOptions Adaptive { get; set; } = new();

	// --- Backward-compatible shims ---

	/// <summary>
	/// Gets or sets a value indicating whether to enable timeout policies for scheduling operations.
	/// </summary>
	/// <value> The current <see cref="EnableTimeoutPolicies" /> value. </value>
	public bool EnableTimeoutPolicies { get => Timeouts.EnableTimeoutPolicies; set => Timeouts.EnableTimeoutPolicies = value; }

	/// <summary>
	/// Gets or sets the timeout for scheduled message retrieval operations.
	/// </summary>
	/// <value> The timeout for scheduled message retrieval operations. </value>
	public TimeSpan ScheduleRetrievalTimeout { get => Timeouts.ScheduleRetrievalTimeout; set => Timeouts.ScheduleRetrievalTimeout = value; }

	/// <summary>
	/// Gets or sets the timeout for message deserialization during scheduling.
	/// </summary>
	/// <value> The timeout for message deserialization during scheduling. </value>
	public TimeSpan DeserializationTimeout { get => Timeouts.DeserializationTimeout; set => Timeouts.DeserializationTimeout = value; }

	/// <summary>
	/// Gets or sets the timeout for scheduled message dispatch operations.
	/// </summary>
	/// <value> The timeout for scheduled message dispatch operations. </value>
	public TimeSpan DispatchTimeout { get => Timeouts.DispatchTimeout; set => Timeouts.DispatchTimeout = value; }

	/// <summary>
	/// Gets or sets the timeout for schedule update operations.
	/// </summary>
	/// <value> The timeout for schedule update operations. </value>
	public TimeSpan ScheduleUpdateTimeout { get => Timeouts.ScheduleUpdateTimeout; set => Timeouts.ScheduleUpdateTimeout = value; }

	/// <summary>
	/// Gets or sets the maximum allowed timeout for any scheduling operation.
	/// </summary>
	/// <value> The maximum allowed timeout for any scheduling operation. </value>
	public TimeSpan MaxSchedulingTimeout { get => Timeouts.MaxSchedulingTimeout; set => Timeouts.MaxSchedulingTimeout = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to apply timeout policies to cron expression evaluation.
	/// </summary>
	/// <value> The current <see cref="EnableCronTimeouts" /> value. </value>
	public bool EnableCronTimeouts { get => Timeouts.EnableCronTimeouts; set => Timeouts.EnableCronTimeouts = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to apply timeout policies to timezone conversions.
	/// </summary>
	/// <value> The current <see cref="EnableTimezoneTimeouts" /> value. </value>
	public bool EnableTimezoneTimeouts { get => Timeouts.EnableTimezoneTimeouts; set => Timeouts.EnableTimezoneTimeouts = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to log timeout events during scheduling operations.
	/// </summary>
	/// <value> The current <see cref="LogSchedulingTimeouts" /> value. </value>
	public bool LogSchedulingTimeouts { get => Timeouts.LogSchedulingTimeouts; set => Timeouts.LogSchedulingTimeouts = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to include timeout metrics in scheduling telemetry.
	/// </summary>
	/// <value> The current <see cref="IncludeTimeoutMetrics" /> value. </value>
	public bool IncludeTimeoutMetrics { get => Timeouts.IncludeTimeoutMetrics; set => Timeouts.IncludeTimeoutMetrics = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable adaptive timeouts based on historical performance.
	/// </summary>
	/// <value> The current <see cref="EnableAdaptiveTimeouts" /> value. </value>
	public bool EnableAdaptiveTimeouts { get => Adaptive.EnableAdaptiveTimeouts; set => Adaptive.EnableAdaptiveTimeouts = value; }

	/// <summary>
	/// Gets or sets the percentile to use for adaptive timeout calculations in scheduling.
	/// </summary>
	/// <value> The percentile to use for adaptive timeout calculations in scheduling. </value>
	public int AdaptiveTimeoutPercentile { get => Adaptive.AdaptiveTimeoutPercentile; set => Adaptive.AdaptiveTimeoutPercentile = value; }

	/// <summary>
	/// Gets or sets the minimum sample size required for adaptive timeouts in scheduling.
	/// </summary>
	/// <value> The minimum sample size required for adaptive timeouts in scheduling. </value>
	public int MinimumSampleSize { get => Adaptive.MinimumSampleSize; set => Adaptive.MinimumSampleSize = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable timeout escalation for failed scheduling operations.
	/// </summary>
	/// <value> The current <see cref="EnableTimeoutEscalation" /> value. </value>
	public bool EnableTimeoutEscalation { get => Adaptive.EnableTimeoutEscalation; set => Adaptive.EnableTimeoutEscalation = value; }

	/// <summary>
	/// Gets or sets the escalation multiplier for retrying timed-out operations.
	/// </summary>
	/// <value> The escalation multiplier for retrying timed-out operations. </value>
	public double TimeoutEscalationMultiplier { get => Adaptive.TimeoutEscalationMultiplier; set => Adaptive.TimeoutEscalationMultiplier = value; }

	/// <summary>
	/// Gets or sets the maximum number of timeout escalations before giving up.
	/// </summary>
	/// <value> The maximum number of timeout escalations before giving up. </value>
	public int MaxTimeoutEscalations { get => Adaptive.MaxTimeoutEscalations; set => Adaptive.MaxTimeoutEscalations = value; }

	/// <summary>
	/// Validates the time-aware scheduler configuration options.
	/// </summary>
	/// <returns> A collection of validation results. </returns>
	public IEnumerable<ValidationResult> Validate()
	{
		var results = new List<ValidationResult>();

		// Validate timeout hierarchy
		if (DeserializationTimeout >= DispatchTimeout)
		{
			results.Add(new ValidationResult(
				ErrorConstants.DeserializationTimeoutShouldBeLessThanDispatchTimeout,
				new[] { nameof(DeserializationTimeout), nameof(DispatchTimeout) }));
		}

		if (ScheduleRetrievalTimeout >= MaxSchedulingTimeout)
		{
			results.Add(new ValidationResult(
				ErrorConstants.ScheduleRetrievalTimeoutShouldBeLessThanMaxSchedulingTimeout,
				new[] { nameof(ScheduleRetrievalTimeout), nameof(MaxSchedulingTimeout) }));
		}

		if (ScheduleUpdateTimeout >= MaxSchedulingTimeout)
		{
			results.Add(new ValidationResult(
				ErrorConstants.ScheduleUpdateTimeoutShouldBeLessThanMaxSchedulingTimeout,
				new[] { nameof(ScheduleUpdateTimeout), nameof(MaxSchedulingTimeout) }));
		}

		if (DispatchTimeout >= MaxSchedulingTimeout)
		{
			results.Add(new ValidationResult(
				ErrorConstants.DispatchTimeoutShouldBeLessThanMaxSchedulingTimeout,
				new[] { nameof(DispatchTimeout), nameof(MaxSchedulingTimeout) }));
		}

		// Validate poll interval relationship
		if (PollInterval >= ScheduleRetrievalTimeout)
		{
			results.Add(new ValidationResult(
				ErrorConstants.PollIntervalShouldBeLessThanScheduleRetrievalTimeout,
				new[] { nameof(PollInterval), nameof(ScheduleRetrievalTimeout) }));
		}

		// Validate message type timeouts
		foreach (var messageTimeout in MessageTypeSchedulingTimeouts)
		{
			if (messageTimeout.Value > MaxSchedulingTimeout)
			{
				results.Add(new ValidationResult(
					string.Format(CultureInfo.InvariantCulture, TimeoutExceedsMaxFormat, messageTimeout.Key),
					new[] { nameof(MessageTypeSchedulingTimeouts) }));
			}

			if (messageTimeout.Value <= TimeSpan.Zero)
			{
				results.Add(new ValidationResult(
					string.Format(CultureInfo.InvariantCulture, TimeoutMustBePositiveFormat, messageTimeout.Key),
					new[] { nameof(MessageTypeSchedulingTimeouts) }));
			}
		}

		// Validate escalation settings
		if (EnableTimeoutEscalation && TimeoutEscalationMultiplier <= 1.0)
		{
			results.Add(new ValidationResult(
				ErrorConstants.TimeoutEscalationMultiplierMustBeGreaterThanOne,
				new[] { nameof(TimeoutEscalationMultiplier), nameof(EnableTimeoutEscalation) }));
		}

		return results;
	}

	/// <summary>
	/// Gets the timeout configuration for a specific operation type in scheduling context.
	/// </summary>
	/// <param name="operationType"> The timeout operation type. </param>
	/// <returns> The configured timeout for the operation type. </returns>
	public TimeSpan GetTimeoutFor(TimeoutOperationType operationType) =>
		operationType switch
		{
			TimeoutOperationType.Database => ScheduleRetrievalTimeout,
			TimeoutOperationType.Serialization => DeserializationTimeout,
			TimeoutOperationType.Handler => DispatchTimeout,
			TimeoutOperationType.Scheduling => ScheduleUpdateTimeout,
			TimeoutOperationType.Validation => DeserializationTimeout,
			TimeoutOperationType.Transport => DispatchTimeout,
			_ => ScheduleRetrievalTimeout,
		};

	/// <summary>
	/// Gets the timeout for a specific message type during scheduling, applying message-specific overrides if configured.
	/// </summary>
	/// <param name="messageType"> The message type. </param>
	/// <param name="operationType"> The operation type. </param>
	/// <returns> The timeout for the message type during scheduling. </returns>
	public TimeSpan GetTimeoutForMessageType(Type messageType, TimeoutOperationType operationType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		var messageTypeName = messageType.FullName ?? messageType.Name;

		if (MessageTypeSchedulingTimeouts.TryGetValue(messageTypeName, out var messageTimeout))
		{
			return messageTimeout;
		}

		return GetTimeoutFor(operationType);
	}

	/// <summary>
	/// Applies complexity multiplier to the base timeout for scheduling operations.
	/// </summary>
	/// <param name="baseTimeout"> The base timeout value. </param>
	/// <param name="complexity"> The operation complexity level. </param>
	/// <returns> The timeout adjusted for complexity. </returns>
	public TimeSpan ApplyComplexityMultiplier(TimeSpan baseTimeout, OperationComplexity complexity)
	{
		var multiplier = complexity switch
		{
			OperationComplexity.Simple => 0.8,
			OperationComplexity.Normal => 1.0,
			OperationComplexity.Complex => ComplexOperationMultiplier,
			OperationComplexity.Heavy => HeavyOperationMultiplier,
			_ => 1.0,
		};

		var adjustedTimeout = TimeSpan.FromTicks((long)(baseTimeout.Ticks * multiplier));

		// Ensure we don't exceed the maximum timeout
		return TimeSpan.FromTicks(Math.Min(adjustedTimeout.Ticks, MaxSchedulingTimeout.Ticks));
	}
}

/// <summary>
/// Timeout value and behavior configuration for the time-aware scheduler.
/// </summary>
public sealed class SchedulerTimeoutOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable timeout policies for scheduling operations.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableTimeoutPolicies { get; set; } = true;

	/// <summary>
	/// Gets or sets the timeout for scheduled message retrieval operations.
	/// </summary>
	/// <value>30 seconds by default.</value>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimeSpan range validation is preserved through explicit validation in Validate() method")]
	[Range(typeof(TimeSpan), "00:00:05", "00:05:00")]
	public TimeSpan ScheduleRetrievalTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the timeout for message deserialization during scheduling.
	/// </summary>
	/// <value>10 seconds by default.</value>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimeSpan range validation is preserved through explicit validation in Validate() method")]
	[Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
	public TimeSpan DeserializationTimeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Gets or sets the timeout for scheduled message dispatch operations.
	/// </summary>
	/// <value>2 minutes by default.</value>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimeSpan range validation is preserved through explicit validation in Validate() method")]
	[Range(typeof(TimeSpan), "00:00:30", "00:10:00")]
	public TimeSpan DispatchTimeout { get; set; } = TimeSpan.FromMinutes(2);

	/// <summary>
	/// Gets or sets the timeout for schedule update operations.
	/// </summary>
	/// <value>15 seconds by default.</value>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimeSpan range validation is preserved through explicit validation in Validate() method")]
	[Range(typeof(TimeSpan), "00:00:05", "00:02:00")]
	public TimeSpan ScheduleUpdateTimeout { get; set; } = TimeSpan.FromSeconds(15);

	/// <summary>
	/// Gets or sets the maximum allowed timeout for any scheduling operation.
	/// </summary>
	/// <value>5 minutes by default.</value>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimeSpan range validation is preserved through explicit validation in Validate() method")]
	[Range(typeof(TimeSpan), "00:01:00", "00:15:00")]
	public TimeSpan MaxSchedulingTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to apply timeout policies to cron expression evaluation.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableCronTimeouts { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to apply timeout policies to timezone conversions.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableTimezoneTimeouts { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to log timeout events during scheduling operations.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool LogSchedulingTimeouts { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include timeout metrics in scheduling telemetry.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool IncludeTimeoutMetrics { get; set; } = true;
}

/// <summary>
/// Adaptive timeout and escalation configuration for the time-aware scheduler.
/// </summary>
public sealed class SchedulerAdaptiveOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable adaptive timeouts based on historical performance.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableAdaptiveTimeouts { get; set; }

	/// <summary>
	/// Gets or sets the percentile to use for adaptive timeout calculations in scheduling.
	/// </summary>
	/// <value>95 by default.</value>
	[Range(75, 99)]
	public int AdaptiveTimeoutPercentile { get; set; } = 95;

	/// <summary>
	/// Gets or sets the minimum sample size required for adaptive timeouts in scheduling.
	/// </summary>
	/// <value>50 by default.</value>
	[Range(10, 1000)]
	public int MinimumSampleSize { get; set; } = 50;

	/// <summary>
	/// Gets or sets a value indicating whether to enable timeout escalation for failed scheduling operations.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableTimeoutEscalation { get; set; } = true;

	/// <summary>
	/// Gets or sets the escalation multiplier for retrying timed-out operations.
	/// </summary>
	/// <value>1.5 by default.</value>
	[Range(1.1, 3.0)]
	public double TimeoutEscalationMultiplier { get; set; } = 1.5;

	/// <summary>
	/// Gets or sets the maximum number of timeout escalations before giving up.
	/// </summary>
	/// <value>3 by default.</value>
	[Range(1, 10)]
	public int MaxTimeoutEscalations { get; set; } = 3;
}
