// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Configuration options for time-based policies in message processing. R7.4: Configurable timeout handling with validation.
/// </summary>
public sealed class TimePolicyOptions
{
	/// <summary>
	/// The configuration section name for time policy options.
	/// </summary>
	public const string SectionName = "Dispatch:TimePolicy";

	/// <summary>
	/// Gets or sets the default timeout for message processing operations.
	/// Default: 30 seconds.
	/// </summary>
	/// <value> The default operation timeout. </value>
	[Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "TimeSpan validation is required for configuration and the type converter is stable")]
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum allowed timeout for any operation.
	/// Default: 5 minutes.
	/// </summary>
	/// <value> The maximum allowed operation timeout. </value>
	[Range(typeof(TimeSpan), "00:00:30", "01:00:00")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "TimeSpan validation is required for configuration and the type converter is stable")]
	public TimeSpan MaxTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the timeout for handler execution.
	/// Default: 2 minutes.
	/// </summary>
	/// <value> The default handler execution timeout. </value>
	[Range(typeof(TimeSpan), "00:00:05", "00:30:00")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "TimeSpan validation is required for configuration and the type converter is stable")]
	public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromMinutes(2);

	/// <summary>
	/// Gets or sets the timeout for serialization operations.
	/// Default: 10 seconds.
	/// </summary>
	/// <value> The serialization timeout. </value>
	[Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "TimeSpan validation is required for configuration and the type converter is stable")]
	public TimeSpan SerializationTimeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Gets or sets the timeout for transport operations.
	/// Default: 1 minute.
	/// </summary>
	/// <value> The transport operation timeout. </value>
	[Range(typeof(TimeSpan), "00:00:05", "00:10:00")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "TimeSpan validation is required for configuration and the type converter is stable")]
	public TimeSpan TransportTimeout { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the timeout for validation operations.
	/// Default: 5 seconds.
	/// </summary>
	/// <value> The validation operation timeout. </value>
	[Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "TimeSpan validation is required for configuration and the type converter is stable")]
	public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the timeout multiplier for complex operations.
	/// Default: 2.0 (doubles the base timeout).
	/// </summary>
	/// <value> The multiplier applied to complex operations. </value>
	[Range(1.0, 10.0)]
	public double ComplexityMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets the timeout multiplier for heavy operations.
	/// Default: 3.0 (triples the base timeout).
	/// </summary>
	/// <value> The multiplier applied to heavy operations. </value>
	[Range(1.0, 10.0)]
	public double HeavyOperationMultiplier { get; set; } = 3.0;

	/// <summary>
	/// Gets or sets a value indicating whether timeouts should be enforced for all operations.
	/// Default: true.
	/// </summary>
	/// <value> True to enforce timeouts; otherwise, false. </value>
	public bool EnforceTimeouts { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to use adaptive timeouts based on historical performance.
	/// Default: false.
	/// </summary>
	/// <value> True to use adaptive timeouts; otherwise, false. </value>
	public bool UseAdaptiveTimeouts { get; set; }

	/// <summary>
	/// Gets or sets the percentile to use for adaptive timeout calculations.
	/// Default: 95th percentile.
	/// </summary>
	/// <value> The percentile used for adaptive timeout calculations. </value>
	[Range(50, 99)]
	public int AdaptiveTimeoutPercentile { get; set; } = 95;

	/// <summary>
	/// Gets or sets the minimum sample size required for adaptive timeouts.
	/// Default: 100 samples.
	/// </summary>
	/// <value> The minimum sample size required for adaptive timeouts. </value>
	[Range(10, 10000)]
	public int MinimumSampleSize { get; set; } = 100;

	/// <summary>
	/// Gets custom timeout overrides for specific operation types.
	/// </summary>
	/// <value> The custom timeout overrides by operation type. </value>
	public IDictionary<TimeoutOperationType, TimeSpan> CustomTimeouts { get; init; } = new Dictionary<TimeoutOperationType, TimeSpan>();

	/// <summary>
	/// Gets custom timeout overrides for specific message types. Key format: "Namespace.MessageTypeName".
	/// </summary>
	/// <value> The custom timeout overrides by message type. </value>
	public IDictionary<string, TimeSpan> MessageTypeTimeouts { get; init; } = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

	/// <summary>
	/// Gets custom timeout overrides for specific handler types. Key format: "Namespace.HandlerTypeName".
	/// </summary>
	/// <value> The custom timeout overrides by handler type. </value>
	public IDictionary<string, TimeSpan> HandlerTypeTimeouts { get; init; } = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets a value indicating whether to log timeout events for monitoring.
	/// Default: true.
	/// </summary>
	/// <value> True to log timeout events; otherwise, false. </value>
	public bool LogTimeoutEvents { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include timeout information in metrics.
	/// Default: true.
	/// </summary>
	/// <value> True to include timeout metrics; otherwise, false. </value>
	public bool IncludeTimeoutMetrics { get; set; } = true;

	/// <summary>
	/// Validates the configuration options.
	/// </summary>
	/// <returns> A collection of validation results. </returns>
	public IEnumerable<ValidationResult> Validate()
	{
		var results = new List<ValidationResult>();
		ValidateSimpleRanges(results);
		ValidateRelativeTimeouts(results);
		ValidateDictionaries(results);
		return results;
	}

	private void ValidateSimpleRanges(List<ValidationResult> results)
	{
		if (DefaultTimeout >= MaxTimeout)
		{
			results.Add(new ValidationResult(
				"DefaultTimeout must be less than MaxTimeout",
				new[] { nameof(DefaultTimeout), nameof(MaxTimeout) }));
		}

		if (HandlerTimeout > MaxTimeout)
		{
			results.Add(new ValidationResult(
				"HandlerTimeout cannot exceed MaxTimeout",
				new[] { nameof(HandlerTimeout), nameof(MaxTimeout) }));
		}

		if (TransportTimeout > MaxTimeout)
		{
			results.Add(new ValidationResult(
				"TransportTimeout cannot exceed MaxTimeout",
				new[] { nameof(TransportTimeout), nameof(MaxTimeout) }));
		}
	}

	private void ValidateRelativeTimeouts(List<ValidationResult> results)
	{
		if (SerializationTimeout >= HandlerTimeout)
		{
			results.Add(new ValidationResult(
				"SerializationTimeout should be less than HandlerTimeout",
				new[] { nameof(SerializationTimeout), nameof(HandlerTimeout) }));
		}

		if (ValidationTimeout >= HandlerTimeout)
		{
			results.Add(new ValidationResult(
				"ValidationTimeout should be less than HandlerTimeout",
				new[] { nameof(ValidationTimeout), nameof(HandlerTimeout) }));
		}
	}

	private void ValidateDictionaries(List<ValidationResult> results)
	{
		foreach (var customTimeout in CustomTimeouts)
		{
			if (customTimeout.Value > MaxTimeout)
			{
				results.Add(new ValidationResult(
					$"Custom timeout for {customTimeout.Key} cannot exceed MaxTimeout",
					new[] { nameof(CustomTimeouts) }));
			}
		}

		foreach (var messageTimeout in MessageTypeTimeouts)
		{
			if (messageTimeout.Value > MaxTimeout)
			{
				results.Add(new ValidationResult(
					$"Message type timeout for {messageTimeout.Key} cannot exceed MaxTimeout",
					new[] { nameof(MessageTypeTimeouts) }));
			}
		}

		foreach (var handlerTimeout in HandlerTypeTimeouts)
		{
			if (handlerTimeout.Value > MaxTimeout)
			{
				results.Add(new ValidationResult(
					$"Handler type timeout for {handlerTimeout.Key} cannot exceed MaxTimeout",
					new[] { nameof(HandlerTypeTimeouts) }));
			}
		}
	}
}
